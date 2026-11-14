using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class LiturgicalDayService(
    ILiturgicalDayRepository liturgicalDayRepository,
    ILiturgicalCalendarClient calendarClient,
    ILiturgicalNameTranslationRepository translationRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IMapper mapper) : ILiturgicalDayService
{
    public async Task<Result<LiturgicalDayDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var liturgicalDay = await liturgicalDayRepository.GetByDateAsync(date, cancellationToken);

        return liturgicalDay is null
            ? Result<LiturgicalDayDto>.Failure(ErrorCodes.CalendarDayNotFound)
            : Result<LiturgicalDayDto>.Success((await ToDtosAsync([liturgicalDay], cancellationToken))[0]);
    }

    public async Task<Result<List<LiturgicalDayDto>>> GetRangeAsync(
        SearchLiturgicalDaysRequest request, CancellationToken cancellationToken)
    {
        var days = await liturgicalDayRepository.GetRangeAsync(request.FromDate, request.ToDate, cancellationToken);

        return Result<List<LiturgicalDayDto>>.Success(await ToDtosAsync(days, cancellationToken));
    }

    // Translated on read, not on sync, so an Admin's edit shows at once without syncing the year again.
    private async Task<List<LiturgicalDayDto>> ToDtosAsync(
        IReadOnlyList<LiturgicalDay> days, CancellationToken cancellationToken)
    {
        var table = await translationRepository.GetAllAsync(cancellationToken);
        var dtos = mapper.Map<List<LiturgicalDayDto>>(days);

        foreach (var dto in dtos)
        {
            dto.CelebrationNameVi = LiturgicalNamePatterns.Translate(LiturgicalNameKind.Celebration, dto.CelebrationName, table);
            dto.RankVi = dto.Rank is null ? null : LiturgicalNamePatterns.Translate(LiturgicalNameKind.Rank, dto.Rank, table);
            dto.SeasonNameVi = dto.SeasonName is null
                ? null
                : LiturgicalNamePatterns.Translate(LiturgicalNameKind.Season, dto.SeasonName, table);
        }

        return dtos;
    }

    public async Task<Result<SyncLiturgicalDaysResponse>> SyncAsync(
        SyncLiturgicalDaysRequest request, CancellationToken cancellationToken)
    {
        var fetched = await calendarClient.GetYearAsync(request.Year, cancellationToken);
        if (!fetched.IsSuccess)
        {
            return Result<SyncLiturgicalDaysResponse>.Failure(fetched.Code!);
        }

        // Overwrite rather than skip: the cache must hold one source, and events match days by date only.
        var cached = (await liturgicalDayRepository.GetYearForUpdateAsync(request.Year, cancellationToken))
            .ToDictionary(x => x.Date);
        var fetchedAt = DateTime.UtcNow;
        var newDays = new List<LiturgicalDay>();
        // Only a day already cached can have changed; a new day had nothing for an event to be planned against.
        var previousNames = new Dictionary<DateOnly, string?>();

        foreach (var day in fetched.Value!)
        {
            if (!cached.TryGetValue(day.Date, out var row))
            {
                row = new LiturgicalDay { Id = Guid.NewGuid(), Date = day.Date };
                newDays.Add(row);
            }
            else if (row.CelebrationName != day.CelebrationName || row.Rank != day.Rank)
            {
                previousNames[day.Date] = row.CelebrationName;
            }

            row.CelebrationName = day.CelebrationName;
            row.Rank = day.Rank;
            row.SeasonName = day.SeasonName;
            row.Color = day.Color;
            row.FetchedAt = fetchedAt;
        }

        await liturgicalDayRepository.AddRangeAsync(newDays, cancellationToken);
        await liturgicalDayRepository.SaveChangesAsync(cancellationToken);

        var affected = await GetAffectedEventsAsync(previousNames, fetched.Value, cancellationToken);
        await NotifyCalendarChangeAsync(affected, cancellationToken);

        return Result<SyncLiturgicalDaysResponse>.Success(
            new SyncLiturgicalDaysResponse { SyncedCount = fetched.Value.Count, AffectedEvents = affected });
    }

    private async Task<List<SyncAffectedEventDto>> GetAffectedEventsAsync(
        Dictionary<DateOnly, string?> previousNames, IReadOnlyCollection<CalendarDayResult> fetched,
        CancellationToken cancellationToken)
    {
        var today = VietnamTime.Today;
        var dates = previousNames.Keys.Where(x => x >= today).ToList();
        if (dates.Count == 0)
        {
            return [];
        }

        var names = fetched.ToDictionary(x => x.Date, x => x.CelebrationName);
        var events = await liturgicalEventRepository.ListAsync(
            x => dates.Contains(x.EventDate) && x.Status != EventStatus.Cancelled, cancellationToken);

        return events
            .OrderBy(x => x.EventDate).ThenBy(x => x.Time)
            .Select(x => new SyncAffectedEventDto
            {
                EventId = x.Id, Title = x.Title, EventDate = x.EventDate, Time = x.Time,
                PreviousCelebrationName = previousNames[x.EventDate], CelebrationName = names[x.EventDate],
            })
            .ToList();
    }

    /// <summary>Tells every Parish Priest which planned events now fall on a different celebration.</summary>
    private async Task NotifyCalendarChangeAsync(List<SyncAffectedEventDto> affected, CancellationToken cancellationToken)
    {
        if (affected.Count == 0)
        {
            return;
        }

        var priestIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ParishPriest], cancellationToken);
        if (priestIds.Count == 0)
        {
            return;
        }

        var dates = string.Join(", ", affected.Select(x => x.EventDate).Distinct().Select(x => x.ToString("dd/MM/yyyy")));
        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.LiturgicalCalendarChanged,
                Title: "Lịch phụng vụ thay đổi",
                Content: $"Lịch phụng vụ vừa cập nhật khác trước ở các ngày {dates}; {affected.Count} sự kiện đã lên kế hoạch cần xem lại.",
                RecipientUserIds: priestIds,
                ReferenceType: null,
                ReferenceId: null),
            cancellationToken);
    }
}

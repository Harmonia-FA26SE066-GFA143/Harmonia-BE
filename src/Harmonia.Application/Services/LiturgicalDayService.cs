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

    public async Task<Result<int>> SyncAsync(SyncLiturgicalDaysRequest request, CancellationToken cancellationToken)
    {
        var fetched = await calendarClient.GetYearAsync(request.Year, cancellationToken);
        if (!fetched.IsSuccess)
        {
            return Result<int>.Failure(fetched.Code!);
        }

        // Overwrite rather than skip: the cache must hold one source, and events match days by date only.
        var cached = (await liturgicalDayRepository.GetYearForUpdateAsync(request.Year, cancellationToken))
            .ToDictionary(x => x.Date);
        var fetchedAt = DateTime.UtcNow;
        var newDays = new List<LiturgicalDay>();

        foreach (var day in fetched.Value!)
        {
            if (!cached.TryGetValue(day.Date, out var row))
            {
                row = new LiturgicalDay { Id = Guid.NewGuid(), Date = day.Date };
                newDays.Add(row);
            }

            row.CelebrationName = day.CelebrationName;
            row.Rank = day.Rank;
            row.SeasonName = day.SeasonName;
            row.FetchedAt = fetchedAt;
        }

        await liturgicalDayRepository.AddRangeAsync(newDays, cancellationToken);
        await liturgicalDayRepository.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(fetched.Value.Count);
    }
}

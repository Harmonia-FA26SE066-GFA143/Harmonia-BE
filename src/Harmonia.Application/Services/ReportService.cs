using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ReportService(
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository) : IReportService
{
    public async Task<Result<PagedList<ServiceHistoryDto>>> GetServiceHistoryAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var page = await liturgicalEventRepository.GetServiceHistoryAsync(request, VietnamTime.Today, cancellationToken);

        var now = DateTime.UtcNow;
        var items = page.Items.Select(e => new ServiceHistoryDto
        {
            EventId = e.Id,
            EventDate = e.EventDate,
            Time = e.Time,
            Title = e.Title,
            LiturgicalSeasonName = e.LiturgicalSeason?.Name,
            LocationName = e.Location.Name,
            SongCount = e.SongLists.Where(s => s.Status == SongListStatus.Approved)
                .OrderByDescending(s => s.Version).Select(s => s.Items.Count).FirstOrDefault(),
            // A roster still in Draft or Suggested is a plan, not service that happened.
            MembersServed = e.ServiceRoster is { Status: RosterStatus.Finalized } roster
                ? roster.Assignments.Where(a => a.Status == RosterAssignmentStatus.Active)
                    .Select(a => a.MemberId).Distinct().Count()
                : 0,
            ParticipationConfirmed = e.EventParticipations.Count(p => p.Status == ParticipationStatus.Confirmed),
            ParticipationDeclined = e.EventParticipations.Count(p => p.Status == ParticipationStatus.Declined),
            RehearsalsHeld = e.Rehearsals.Count(r => r.StartTime <= now),
            AttendancePresent = e.Rehearsals.Where(r => r.StartTime <= now).Sum(r => r.Attendances.Count(
                a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late)),
        }).ToList();

        return Result<PagedList<ServiceHistoryDto>>.Success(
            new PagedList<ServiceHistoryDto>(items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<PagedList<SongUsageDto>>> GetSongUsageAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var page = await songRepository.GetUsageAsync(request, VietnamTime.Today, cancellationToken);

        var items = page.Items.Select(x => new SongUsageDto
        {
            SongId = x.Song.Id,
            Title = x.Song.Title,
            Composer = x.Song.Composer,
            TimesUsed = x.TimesUsed,
            LastUsedDate = x.LastUsedDate,
        }).ToList();

        return Result<PagedList<SongUsageDto>>.Success(
            new PagedList<SongUsageDto>(items, page.PageNumber, page.PageSize, page.TotalCount));
    }
}

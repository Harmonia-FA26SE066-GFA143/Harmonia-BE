using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ReportService(
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository,
    IUserRepository userRepository,
    IMemberProfileRepository memberProfileRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository) : IReportService
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

    public async Task<Result<PagedList<UserActivityDto>>> GetUserActivityAsync(
        SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToPage(await userRepository.GetActivityAsync(request, cancellationToken), x => new UserActivityDto
        {
            UserId = x.User.Id,
            FullName = x.User.FullName,
            Email = x.User.Email,
            RoleName = x.User.Role.Name,
            IsActive = x.User.IsActive,
            LastLoginAt = x.User.LastLoginAt,
            PasswordChangeCount = x.PasswordChangeCount,
            PasswordResetCount = x.PasswordResetCount,
            AccountChangeCount = x.AccountChangeCount,
            ActionCount = x.ActionCount,
        });

    public async Task<Result<PagedList<RehearsalAttendanceReportDto>>> GetRehearsalAttendanceAsync(
        SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToPage(
            await memberProfileRepository.GetActiveWithActivityAsync(request, cancellationToken),
            member => new RehearsalAttendanceReportDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                PresentCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Present),
                LateCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Late),
                AbsentCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Absent),
                ExcusedCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Excused),
            });

    public async Task<Result<PagedList<ParticipationReportDto>>> GetParticipationAsync(
        SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToPage(
            await memberProfileRepository.GetActiveWithActivityAsync(request, cancellationToken),
            member => new ParticipationReportDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                InvitedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Invited),
                ConfirmedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Confirmed),
                DeclinedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Declined),
                UnsureCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Unsure),
            });

    public async Task<Result<PagedList<PracticeAssignmentCompletionDto>>> GetAssignmentCompletionAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var page = await memberProfileRepository.GetActiveWithActivityAsync(request, cancellationToken);
        var assignments = await practiceAssignmentRepository.GetForReportAsync(
            request, page.Items.Select(m => m.Id).ToList(), cancellationToken);

        return ToPage(page, member =>
        {
            var latestStatuses = assignments
                .Where(a => EventPreparationService.Receives(a, member))
                .Select(a => a.Submissions.Where(s => s.MemberId == member.Id)
                    .OrderByDescending(s => s.AttemptNo).Select(s => (SubmissionStatus?)s.Status).FirstOrDefault())
                .ToList();

            return new PracticeAssignmentCompletionDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                AssignedCount = latestStatuses.Count,
                PassedCount = latestStatuses.Count(s => s == SubmissionStatus.Passed),
                NeedsRevisionCount = latestStatuses.Count(s => s == SubmissionStatus.NeedsRevision),
                AwaitingReviewCount = latestStatuses.Count(s => s == SubmissionStatus.Submitted),
                NotSubmittedCount = latestStatuses.Count(s => s is null or SubmissionStatus.Overdue),
            };
        });
    }

    private static Result<PagedList<TDto>> ToPage<TSource, TDto>(PagedList<TSource> page, Func<TSource, TDto> map) =>
        Result<PagedList<TDto>>.Success(
            new PagedList<TDto>(page.Items.Select(map).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ReportService(
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository,
    IUserRepository userRepository,
    IMemberProfileRepository memberProfileRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository,
    ICurrentUserService currentUser,
    IChoirScopeService choirScope) : IReportService
{
    // The page size cap of PagingRequest; the trend walks every page of members.
    private const int TrendPageSize = 100;

    /// <summary>
    /// The per-member reports of a Choir Director cover only the members they manage (D8), and a choirId filter must
    /// be one of their own choirs (CHOIR_NOT_FOUND otherwise). Other roles see every member.
    /// </summary>
    private async Task<(Guid? DirectorUserId, string? Error)> GetMemberScopeAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        if (await choirScope.GetDirectedChoirIdsAsync(cancellationToken) is not { } directed)
        {
            return (null, null);
        }

        return request.ChoirId is { } choirId && !directed.Contains(choirId)
            ? (null, ErrorCodes.ChoirNotFound)
            : (currentUser.UserId, null);
    }

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
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var (directorUserId, scopeError) = await GetMemberScopeAsync(request, cancellationToken);
        if (scopeError is not null) return Result<PagedList<RehearsalAttendanceReportDto>>.Failure(scopeError);

        return ToPage(
            await memberProfileRepository.GetActiveWithActivityAsync(request, directorUserId, cancellationToken),
            member => new RehearsalAttendanceReportDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                PresentCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Present),
                LateCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Late),
                AbsentCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Absent),
                ExcusedCount = member.RehearsalAttendances.Count(a => a.Status == AttendanceStatus.Excused),
            });
    }

    public async Task<Result<PagedList<ParticipationReportDto>>> GetParticipationAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var (directorUserId, scopeError) = await GetMemberScopeAsync(request, cancellationToken);
        if (scopeError is not null) return Result<PagedList<ParticipationReportDto>>.Failure(scopeError);

        return ToPage(
            await memberProfileRepository.GetActiveWithActivityAsync(request, directorUserId, cancellationToken),
            member => new ParticipationReportDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                InvitedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Invited),
                ConfirmedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Confirmed),
                DeclinedCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Declined),
                UnsureCount = member.EventParticipations.Count(p => p.Status == ParticipationStatus.Unsure),
            });
    }

    public async Task<Result<PagedList<PracticeAssignmentCompletionDto>>> GetAssignmentCompletionAsync(
        SearchReportsRequest request, CancellationToken cancellationToken)
    {
        var (directorUserId, scopeError) = await GetMemberScopeAsync(request, cancellationToken);
        if (scopeError is not null) return Result<PagedList<PracticeAssignmentCompletionDto>>.Failure(scopeError);

        var page = await memberProfileRepository.GetActiveWithActivityAsync(request, directorUserId, cancellationToken);
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

    public async Task<Result<List<ActivityTrendDto>>> GetActivityTrendAsync(
        SearchActivityTrendRequest request, CancellationToken cancellationToken)
    {
        var (from, to) = (request.FromDate!.Value, request.ToDate!.Value);
        var filter = new SearchReportsRequest
        {
            ChoirId = request.ChoirId, FromDate = from, ToDate = to, PageSize = TrendPageSize,
        };

        var (directorUserId, scopeError) = await GetMemberScopeAsync(filter, cancellationToken);
        if (scopeError is not null) return Result<List<ActivityTrendDto>>.Failure(scopeError);

        var members = new List<MemberProfile>();
        for (filter.PageNumber = 1; ; filter.PageNumber++)
        {
            var page = await memberProfileRepository.GetActiveWithActivityAsync(filter, directorUserId, cancellationToken);
            members.AddRange(page.Items);
            if (page.Items.Count == 0 || members.Count >= page.TotalCount) break;
        }

        var assignments = await practiceAssignmentRepository.GetForReportAsync(
            filter, members.Select(m => m.Id).ToList(), cancellationToken);

        var attendances = members
            .SelectMany(m => m.RehearsalAttendances)
            .Select(a => (Day: DateOnly.FromDateTime(VietnamTime.FromUtc(a.Rehearsal.StartTime)), a.RehearsalId, a.Status))
            .ToList();
        var received = assignments
            .SelectMany(a => members.Where(m => EventPreparationService.Receives(a, m)).Select(m => (
                Day: DateOnly.FromDateTime(VietnamTime.FromUtc(a.DueDate)),
                AssignmentId: a.Id,
                Latest: a.Submissions.Where(s => s.MemberId == m.Id)
                    .OrderByDescending(s => s.AttemptNo).Select(s => (SubmissionStatus?)s.Status).FirstOrDefault())))
            .ToList();

        var rows = Periods(request.GroupBy, from, to).Select(period =>
        {
            var inAttendance = attendances.Where(a => a.Day >= period.Start && a.Day <= period.End).ToList();
            var inPractice = received.Where(r => r.Day >= period.Start && r.Day <= period.End).ToList();
            var attended = inAttendance.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            var passed = inPractice.Count(r => r.Latest == SubmissionStatus.Passed);

            return new ActivityTrendDto
            {
                PeriodStart = period.Start,
                PeriodEnd = period.End,
                RehearsalsRecorded = inAttendance.Select(a => a.RehearsalId).Distinct().Count(),
                PresentCount = inAttendance.Count(a => a.Status == AttendanceStatus.Present),
                LateCount = inAttendance.Count(a => a.Status == AttendanceStatus.Late),
                AbsentCount = inAttendance.Count(a => a.Status == AttendanceStatus.Absent),
                ExcusedCount = inAttendance.Count(a => a.Status == AttendanceStatus.Excused),
                AttendanceRate = inAttendance.Count == 0 ? null : (double)attended / inAttendance.Count,
                AssignmentsDue = inPractice.Select(r => r.AssignmentId).Distinct().Count(),
                AssignedCount = inPractice.Count,
                PassedCount = passed,
                NeedsRevisionCount = inPractice.Count(r => r.Latest == SubmissionStatus.NeedsRevision),
                AwaitingReviewCount = inPractice.Count(r => r.Latest == SubmissionStatus.Submitted),
                NotSubmittedCount = inPractice.Count(r => r.Latest is null or SubmissionStatus.Overdue),
                CompletionRate = inPractice.Count == 0 ? null : (double)passed / inPractice.Count,
            };
        }).ToList();

        return Result<List<ActivityTrendDto>>.Success(rows);
    }

    /// <summary>Consecutive periods covering <paramref name="from"/>..<paramref name="to"/>, clipped to that range.</summary>
    private static IEnumerable<(DateOnly Start, DateOnly End)> Periods(ReportPeriod groupBy, DateOnly from, DateOnly to)
    {
        var start = groupBy switch
        {
            // DayOfWeek counts from Sunday; weeks here start on Monday.
            ReportPeriod.Week => from.AddDays(-(((int)from.DayOfWeek + 6) % 7)),
            ReportPeriod.Month => new DateOnly(from.Year, from.Month, 1),
            _ => new DateOnly(from.Year, 1, 1),
        };

        while (start <= to)
        {
            var next = groupBy switch
            {
                ReportPeriod.Week => start.AddDays(7),
                ReportPeriod.Month => start.AddMonths(1),
                _ => start.AddYears(1),
            };
            yield return (start < from ? from : start, next.AddDays(-1) > to ? to : next.AddDays(-1));
            start = next;
        }
    }

    private static Result<PagedList<TDto>> ToPage<TSource, TDto>(PagedList<TSource> page, Func<TSource, TDto> map) =>
        Result<PagedList<TDto>>.Success(
            new PagedList<TDto>(page.Items.Select(map).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
}

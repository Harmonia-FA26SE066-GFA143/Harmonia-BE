using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class PracticeAssignmentRepository(HarmoniaDbContext dbContext)
    : GenericRepository<PracticeAssignment>(dbContext), IPracticeAssignmentRepository
{
    public async Task<PagedList<PracticeAssignment>> GetForMemberAsync(
        Guid memberId, SearchMyPracticeAssignmentsRequest filter, CancellationToken cancellationToken)
    {
        var query = VisibleToMember(memberId);

        if (filter.IsOpen is { } isOpen)
        {
            var now = DateTime.UtcNow;
            query = isOpen ? query.Where(x => x.DueDate >= now) : query.Where(x => x.DueDate < now);
        }

        if (filter.HasSubmission is { } hasSubmission)
            query = query.Where(x => x.Submissions.Any(s => s.MemberId == memberId) == hasSubmission);

        // "Newest attempt" is spelled out in each Where: EF cannot inline a shared C# helper into SQL.
        if (filter.Status == SubmissionStatus.Overdue)
        {
            // SQL copy of PracticeAssignment.IsOverdue; a repository test keeps the two in step.
            var now = DateTime.UtcNow;
            query = query.Where(x => x.DueDate < now
                && x.Submissions.Where(s => s.MemberId == memberId).OrderByDescending(s => s.AttemptNo)
                    .Select(s => (SubmissionStatus?)s.Status).FirstOrDefault() != SubmissionStatus.Passed);
        }
        else if (filter.Status is { } status)
        {
            query = query.Where(x => x.Submissions.Where(s => s.MemberId == memberId).OrderByDescending(s => s.AttemptNo)
                .Select(s => (SubmissionStatus?)s.Status).FirstOrDefault() == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await WithDetails(query, memberId)
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<PracticeAssignment>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<PagedList<PracticeAssignment>> SearchAsync(
        SearchPracticeAssignmentsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.PracticeAssignments.AsNoTracking();

        if (filter.EventId is { } eventId)
            query = query.Where(x => x.EventId == eventId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Targets)
            .OrderByDescending(x => x.DueDate)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<PracticeAssignment>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<List<(DateTime DueDate, SubmissionStatus? LatestStatus)>> GetProgressForMemberAsync(
        Guid memberId, CancellationToken cancellationToken)
    {
        var rows = await VisibleToMember(memberId)
            .Select(x => new
            {
                x.DueDate,
                LatestStatus = x.Submissions.Where(s => s.MemberId == memberId).OrderByDescending(s => s.AttemptNo)
                    .Select(s => (SubmissionStatus?)s.Status).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => (x.DueDate, x.LatestStatus)).ToList();
    }

    public Task<PracticeAssignment?> GetByIdForMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken) =>
        WithDetails(VisibleToMember(memberId), memberId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<PracticeAssignment>> GetByEventWithSubmissionsAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.PracticeAssignments
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Include(x => x.Targets)
            .Include(x => x.Submissions)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<List<PracticeAssignment>> GetForMemberByEventsAsync(
        Guid memberId, IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken) =>
        VisibleToMember(memberId)
            .Where(x => x.EventId != null && eventIds.Contains(x.EventId.Value))
            .Include(x => x.Submissions.Where(s => s.MemberId == memberId).OrderByDescending(s => s.AttemptNo).Take(1))
            .ToListAsync(cancellationToken);

    public Task<List<PracticeAssignment>> GetForReportAsync(
        SearchReportsRequest filter, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken)
    {
        var (eventId, seasonId) = (filter.EventId, filter.LiturgicalSeasonId);
        DateTime? fromUtc = filter.FromDate is { } from ? VietnamTime.StartOfDayUtc(from) : null;
        DateTime? toUtc = filter.ToDate is { } to ? VietnamTime.StartOfDayUtc(to.AddDays(1)) : null;

        return DbContext.PracticeAssignments
            .AsNoTracking()
            .Where(x => (fromUtc == null || x.DueDate >= fromUtc)
                && (toUtc == null || x.DueDate < toUtc)
                && (eventId == null || x.EventId == eventId)
                && (seasonId == null || x.LiturgicalEvent!.LiturgicalSeasonId == seasonId))
            .Include(x => x.Targets)
            .Include(x => x.Submissions.Where(s => memberIds.Contains(s.MemberId)))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Scope All, a target naming the member, or a target skill the member holds approved today. Skill targets
    /// follow the member's current skills, so a newly approved skill also brings its earlier assignments.
    /// EventPreparationService.Receives repeats this rule in memory; change both together.
    /// </summary>
    private IQueryable<PracticeAssignment> VisibleToMember(Guid memberId) =>
        DbContext.PracticeAssignments
            .AsNoTracking()
            .Where(x => x.Scope == AssignmentScope.All
                || x.Targets.Any(t => t.MemberId == memberId
                    || DbContext.MemberSkills.Any(ms => ms.MemberId == memberId
                        && ms.SkillId == t.SkillId
                        && ms.Status == ApprovalStatus.Approved)));

    private static IQueryable<PracticeAssignment> WithDetails(IQueryable<PracticeAssignment> query, Guid memberId) =>
        query
            .Include(x => x.LiturgicalEvent)
            .Include(x => x.Song)
            .Include(x => x.Material)
            .Include(x => x.Submissions.Where(s => s.MemberId == memberId).OrderByDescending(s => s.AttemptNo).Take(1))
            .AsSplitQuery();
}

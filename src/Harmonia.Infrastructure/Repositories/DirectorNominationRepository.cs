using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class DirectorNominationRepository(HarmoniaDbContext dbContext)
    : GenericRepository<DirectorNomination>(dbContext), IDirectorNominationRepository
{
    private static readonly DirectorNominationStatus[] OpenStatuses =
    [
        DirectorNominationStatus.Pending, DirectorNominationStatus.Approved,
        DirectorNominationStatus.InTraining, DirectorNominationStatus.Trained,
    ];

    public Task<DirectorNomination?> GetWithDetailsAsync(
        Guid id, Guid? nominatedById, Guid? memberUserId, CancellationToken cancellationToken) =>
        Visible(nominatedById, memberUserId).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedList<DirectorNomination>> SearchAsync(
        SearchDirectorNominationsRequest filter, Guid? nominatedById, Guid? memberUserId,
        CancellationToken cancellationToken)
    {
        var query = Visible(nominatedById, memberUserId);

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<DirectorNomination>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<bool> HasOpenAsync(Guid memberId, CancellationToken cancellationToken) =>
        DbContext.DirectorNominations.AnyAsync(
            x => x.MemberId == memberId && OpenStatuses.Contains(x.Status), cancellationToken);

    public async Task<MemberServiceStats> GetMemberStatsAsync(
        Guid memberId, DateTime attendanceSinceUtc, DateOnly today, CancellationToken cancellationToken)
    {
        var firstJoined = await DbContext.ChoirMemberships
            .Where(x => x.MemberId == memberId)
            .MinAsync(x => (DateOnly?)x.JoinedDate, cancellationToken);

        var attendances = DbContext.RehearsalAttendances
            .Where(x => x.MemberId == memberId && x.Rehearsal.StartTime >= attendanceSinceUtc);
        var recorded = await attendances.CountAsync(cancellationToken);
        var present = await attendances.CountAsync(
            x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late, cancellationToken);

        var served = await DbContext.RosterAssignments
            .Where(x => x.MemberId == memberId
                && x.Status == RosterAssignmentStatus.Active
                && x.Roster.Status == RosterStatus.Finalized
                && x.Roster.LiturgicalEvent.Status != EventStatus.Cancelled
                && x.Roster.LiturgicalEvent.EventDate <= today)
            .Select(x => x.Roster.EventId)
            .Distinct()
            .CountAsync(cancellationToken);

        var hasConductingSkill = await DbContext.MemberSkills.AnyAsync(
            x => x.MemberId == memberId && x.Status == ApprovalStatus.Approved
                && x.Skill.CategoryId == SkillCategoryIds.ConductingSupport,
            cancellationToken);

        return new MemberServiceStats(firstJoined, recorded, present, served, hasConductingSkill);
    }

    /// <summary>
    /// A member only sees a nomination the Parish Priest approved at some point: never one still pending, rejected or
    /// withdrawn before a decision.
    /// </summary>
    private IQueryable<DirectorNomination> Visible(Guid? nominatedById, Guid? memberUserId)
    {
        var query = DbContext.DirectorNominations
            .AsNoTracking()
            .Include(x => x.Member).ThenInclude(x => x.User)
            .Include(x => x.NominatedBy)
            .Include(x => x.ProposedChoir)
            .Include(x => x.AppointedChoir)
            .AsQueryable();

        if (nominatedById is { } nominator)
        {
            query = query.Where(x => x.NominatedById == nominator);
        }

        if (memberUserId is { } userId)
        {
            query = query.Where(x => x.Member.UserId == userId
                && x.ReviewedAt != null && x.Status != DirectorNominationStatus.Rejected);
        }

        return query;
    }
}

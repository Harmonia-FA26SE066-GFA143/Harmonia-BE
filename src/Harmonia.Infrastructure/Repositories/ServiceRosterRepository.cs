using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class ServiceRosterRepository(HarmoniaDbContext dbContext)
    : GenericRepository<ServiceRoster>(dbContext), IServiceRosterRepository
{
    public Task<LiturgicalEvent?> GetEventWithRosterAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents
            .Include(x => x.ServiceRoster).ThenInclude(x => x!.Assignments)
            .FirstOrDefaultAsync(x => x.Id == eventId, cancellationToken);

    public Task<SongList?> GetApprovedSongListAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .AsNoTracking()
            .Include(x => x.Items).ThenInclude(x => x.Song)
            .Include(x => x.Items).ThenInclude(x => x.PersonnelRequirements).ThenInclude(x => x.Skill)
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.Status == SongListStatus.Approved, cancellationToken);

    public Task<List<MemberSkill>> GetCandidateSkillsAsync(
        Guid eventId, IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken) =>
        DbContext.MemberSkills
            .AsNoTracking()
            .Where(x => x.Status == ApprovalStatus.Approved
                && skillIds.Contains(x.SkillId)
                && x.Member.Status == MemberStatus.Active
                && x.Member.EventParticipations.Any(p => p.EventId == eventId && p.Status == ParticipationStatus.Confirmed))
            .ToListAsync(cancellationToken);

    public Task<Dictionary<Guid, int>> CountRecentServicesAsync(
        IReadOnlyCollection<Guid> memberIds, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        DbContext.RosterAssignments
            .Where(x => memberIds.Contains(x.MemberId)
                && x.Status == RosterAssignmentStatus.Active
                && x.Roster.LiturgicalEvent.EventDate >= from
                && x.Roster.LiturgicalEvent.EventDate < to)
            .GroupBy(x => x.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Select(x => x.RosterId).Distinct().Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, cancellationToken);

    public Task<List<RosterAssignment>> GetAssignmentsAsync(Guid rosterId, CancellationToken cancellationToken) =>
        DbContext.RosterAssignments
            .AsNoTracking()
            .Include(x => x.Member).ThenInclude(x => x.User)
            .Include(x => x.Skill)
            .Include(x => x.SongListItem).ThenInclude(x => x!.Song)
            .Where(x => x.RosterId == rosterId && x.Status == RosterAssignmentStatus.Active)
            .OrderBy(x => x.SongListItem!.DisplayOrder).ThenBy(x => x.Skill.Name).ThenBy(x => x.Member.User.FullName)
            .ToListAsync(cancellationToken);

    public Task<MemberProfile?> GetMemberForAssignmentAsync(
        Guid memberId, Guid skillId, Guid eventId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .AsNoTracking()
            .Include(x => x.MemberSkills.Where(s => s.SkillId == skillId && s.Status == ApprovalStatus.Approved))
            .Include(x => x.EventParticipations.Where(p => p.EventId == eventId && p.Status == ParticipationStatus.Confirmed))
            .FirstOrDefaultAsync(x => x.Id == memberId, cancellationToken);

    public Task<ServiceRoster?> GetRosterByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        DbContext.ServiceRosters
            .Include(x => x.LiturgicalEvent)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Assignments.Any(a => a.Id == assignmentId), cancellationToken);
}

using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IServiceRosterRepository : IGenericRepository<ServiceRoster>
{
    /// <summary>Tracked event with its ServiceRoster and the roster's assignments loaded (roster may be null).</summary>
    Task<LiturgicalEvent?> GetEventWithRosterAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only approved song list of the event, with each item's Song and its personnel requirements
    /// (each with its Skill) loaded. Null when the event has no approved version.
    /// </summary>
    Task<SongList?> GetApprovedSongListAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only approved declarations of the given skills held by Active members who confirmed
    /// participation in the event.
    /// </summary>
    Task<List<MemberSkill>> GetCandidateSkillsAsync(
        Guid eventId, IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken);

    /// <summary>
    /// Number of distinct events each member is on a roster for, dated from <paramref name="from"/>
    /// up to but excluding <paramref name="to"/>. Members with none are absent from the result.
    /// </summary>
    Task<Dictionary<Guid, int>> CountRecentServicesAsync(
        IReadOnlyCollection<Guid> memberIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Read-only Active assignments of the roster, with Member.User, Skill and SongListItem.Song loaded.</summary>
    Task<List<RosterAssignment>> GetAssignmentsAsync(Guid rosterId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only member with only their approved declaration of <paramref name="skillId"/> and their
    /// confirmed participation in <paramref name="eventId"/> loaded (either collection may be empty).
    /// </summary>
    Task<MemberProfile?> GetMemberForAssignmentAsync(
        Guid memberId, Guid skillId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>Tracked roster owning the assignment, with its LiturgicalEvent and all its assignments loaded.</summary>
    Task<ServiceRoster?> GetRosterByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Tracked roster with its LiturgicalEvent loaded (assignments not loaded).</summary>
    Task<ServiceRoster?> GetRosterWithEventAsync(Guid rosterId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only Active assignments of the roster, loaded like <see cref="GetAssignmentsAsync"/> plus, on each Member,
    /// only their approved skill declarations and their confirmed participation in <paramref name="eventId"/>.
    /// </summary>
    Task<List<RosterAssignment>> GetAssignmentsWithEligibilityAsync(
        Guid rosterId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked roster with its LiturgicalEvent and only its Active assignments loaded, each with Member, Skill
    /// and SongListItem.Song.
    /// </summary>
    Task<ServiceRoster?> GetRosterForNotificationAsync(Guid rosterId, CancellationToken cancellationToken);
}

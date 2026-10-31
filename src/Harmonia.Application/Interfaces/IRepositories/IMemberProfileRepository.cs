using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberProfileRepository : IGenericRepository<MemberProfile>
{
    /// <summary>Read-only, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only, with <see cref="MemberProfile.User"/> and its Role loaded. <see cref="MemberProfile.MemberSkills"/>
    /// holds only approved skills that are still active, with <see cref="Skill.Category"/> loaded.
    /// </summary>
    Task<MemberProfile?> GetByUserIdWithApprovedSkillsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetWithUserAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// True when the member exists and the Choir Director manages them (D8): the member is active in a choir the
    /// director leads, or is in no choir yet. A choir that was deactivated still counts.
    /// </summary>
    Task<bool> IsManagedByDirectorAsync(Guid memberId, Guid directorUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of members with their User, ordered by name. <paramref name="keyword"/> matches
    /// part of the name or email; the status and skill of <paramref name="filter"/> narrow the result when set.
    /// <see cref="MemberProfile.MemberSkills"/> holds only approved skills that are still active,
    /// with <see cref="Skill.Category"/> loaded.
    /// </summary>
    Task<PagedList<MemberProfile>> SearchAsync(
        string? keyword, SearchMemberProfilesRequest filter, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of active members expected to learn the material, ordered by name: everyone when
    /// <paramref name="targetSkillId"/> is null, otherwise those with that skill approved.
    /// <see cref="MemberProfile.LearningProgresses"/> holds only the row for this material, if any.
    /// </summary>
    /// <remarks>Only members the Choir Director manages (see <see cref="IsManagedByDirectorAsync"/>).</remarks>
    Task<PagedList<MemberProfile>> GetLearnersOfMaterialAsync(
        Guid materialId, Guid? targetSkillId, Guid directorUserId, SearchMaterialLearningProgressRequest filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read-only, ordered by name: active members (of <paramref name="choirId"/> when set) plus anyone already recorded
    /// at the rehearsal, so history survives a member leaving. <see cref="MemberProfile.User"/> is loaded and
    /// <see cref="MemberProfile.RehearsalAttendances"/> holds only the row for this rehearsal, if any.
    /// </summary>
    Task<List<MemberProfile>> GetAttendanceRosterAsync(
        Guid rehearsalId, Guid? choirId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only active members, ordered by name, with <see cref="MemberProfile.User"/> loaded; when choirs serve the
    /// event (D8), only their members. <see cref="MemberProfile.MemberSkills"/> holds only approved skills,
    /// <see cref="MemberProfile.ChoirMemberships"/> only active rows and
    /// <see cref="MemberProfile.EventParticipations"/> only the row for this event, if any.
    /// </summary>
    Task<List<MemberProfile>> GetActiveForEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of active members, ordered by name, with <see cref="MemberProfile.User"/> loaded and
    /// <see cref="MemberProfile.MemberSkills"/> holding only approved skills. The event, season and date range of
    /// <paramref name="filter"/> narrow the loaded rows, not the members:
    /// <see cref="MemberProfile.RehearsalAttendances"/> holds rows of rehearsals starting inside the range (each with its
    /// Rehearsal), and
    /// <see cref="MemberProfile.EventParticipations"/> rows of Published events dated inside it.
    /// A rehearsal without an event matches no season. <see cref="MemberProfile.ChoirMemberships"/> holds active rows.
    /// With <paramref name="directorUserId"/>, only members that director manages (see <see cref="IsManagedByDirectorAsync"/>).
    /// </summary>
    Task<PagedList<MemberProfile>> GetActiveWithActivityAsync(
        SearchReportsRequest filter, Guid? directorUserId, CancellationToken cancellationToken);

    /// <summary>Read-only members with the given ids, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<List<MemberProfile>> GetByIdsWithUserAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Read-only active members, with <see cref="MemberProfile.User"/> loaded, ordered by name.</summary>
    Task<List<MemberProfile>> GetAllActiveWithUserAsync(CancellationToken cancellationToken);
}

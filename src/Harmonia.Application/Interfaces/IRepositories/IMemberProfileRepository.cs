using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberProfileRepository : IGenericRepository<MemberProfile>
{
    /// <summary>Read-only, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetWithUserAsync(Guid id, CancellationToken cancellationToken);

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
    Task<PagedList<MemberProfile>> GetLearnersOfMaterialAsync(
        Guid materialId, Guid? targetSkillId, SearchMaterialLearningProgressRequest filter, CancellationToken cancellationToken);
}

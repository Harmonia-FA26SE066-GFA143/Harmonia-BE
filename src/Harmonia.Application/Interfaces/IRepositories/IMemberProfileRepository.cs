using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberProfileRepository : IGenericRepository<MemberProfile>
{
    /// <summary>Read-only, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of active members expected to learn the material, ordered by name: everyone when
    /// <paramref name="targetSkillId"/> is null, otherwise those with that skill approved.
    /// <see cref="MemberProfile.LearningProgresses"/> holds only the row for this material, if any.
    /// </summary>
    Task<PagedList<MemberProfile>> GetLearnersOfMaterialAsync(
        Guid materialId, Guid? targetSkillId, SearchMaterialLearningProgressRequest filter, CancellationToken cancellationToken);
}

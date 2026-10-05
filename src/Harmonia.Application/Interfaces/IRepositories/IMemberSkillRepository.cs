using Harmonia.Application.Common.Models;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberSkillRepository : IGenericRepository<MemberSkill>
{
    /// <summary>Read-only, with <see cref="Skill.Category"/> loaded.</summary>
    Task<Skill?> GetSkillWithCategoryAsync(Guid skillId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the member already holds a Pending or Approved declaration of the skill.
    /// Rejected rows are history and do not block a new declaration.
    /// </summary>
    Task<bool> HasActiveDeclarationAsync(Guid memberId, Guid skillId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the declaration and saves. Returns false when a concurrent request inserted a
    /// Pending or Approved declaration of the same skill first, so the unique index rejected this one.
    /// </summary>
    Task<bool> TryAddAsync(MemberSkill memberSkill, CancellationToken cancellationToken);

    /// <summary>Read-only, with <see cref="MemberSkill.Skill"/> and its Category loaded.</summary>
    Task<MemberSkill?> GetWithSkillAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of the member's declarations, rejected history included, newest first,
    /// with <see cref="MemberSkill.Skill"/> and its Category loaded. A null status returns every status.
    /// </summary>
    Task<PagedList<MemberSkill>> GetByMemberAsync(
        Guid memberId, ApprovalStatus? status, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of every Pending declaration, oldest first, with <see cref="MemberSkill.Skill"/>
    /// and its Category, and <see cref="MemberSkill.Member"/> and its User loaded.
    /// </summary>
    Task<PagedList<MemberSkill>> GetPendingAsync(PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked, with <see cref="MemberSkill.Skill"/> and its Category, and <see cref="MemberSkill.Member"/>
    /// and its User loaded.
    /// </summary>
    Task<MemberSkill?> GetForReviewAsync(Guid id, CancellationToken cancellationToken);
}

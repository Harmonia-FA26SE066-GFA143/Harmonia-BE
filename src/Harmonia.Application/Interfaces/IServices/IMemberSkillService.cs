using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Skills declared by choir members and their approval (UC-03 / FE-03, FE-04).</summary>
public interface IMemberSkillService
{
    /// <summary>
    /// The calling member declares a skill, which starts as Pending until the Choir Director reviews it.
    /// A skill that was rejected before can be declared again: the rejected row stays as history and
    /// a new Pending row is added. A Pending or Approved declaration of the same skill fails with
    /// MEMBER_SKILL_ALREADY_DECLARED.
    /// </summary>
    Task<Result<MemberSkillDto>> DeclareAsync(
        Guid userId, DeclareMemberSkillRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The calling member's declarations with their status, rejected history included, newest first.
    /// A null status returns every status.
    /// </summary>
    Task<Result<PagedList<MemberSkillDto>>> GetMineAsync(
        Guid userId, ApprovalStatus? status, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// One declaration of the calling member. Another member's declaration fails with
    /// MEMBER_SKILL_NOT_FOUND, so its existence stays hidden.
    /// </summary>
    Task<Result<MemberSkillDto>> GetMineByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>Pending declarations of every member, oldest first, for the Choir Director to review (UC-19).</summary>
    Task<Result<PagedList<MemberSkillDetailDto>>> GetPendingAsync(PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// The Choir Director approves a Pending declaration and the member is notified. A declaration that
    /// is no longer Pending fails with MEMBER_SKILL_ALREADY_REVIEWED.
    /// </summary>
    Task<Result<MemberSkillDetailDto>> ApproveAsync(Guid directorUserId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The Choir Director rejects a Pending declaration with a reason and the member is notified.
    /// A declaration that is no longer Pending fails with MEMBER_SKILL_ALREADY_REVIEWED.
    /// </summary>
    Task<Result<MemberSkillDetailDto>> RejectAsync(
        Guid directorUserId, Guid id, RejectMemberSkillRequest request, CancellationToken cancellationToken);
}

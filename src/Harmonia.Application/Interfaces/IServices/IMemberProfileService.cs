using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IMemberProfileService
{
    /// <summary>Profile of the calling user; fails with MEMBER_NOT_FOUND if the account has none.</summary>
    Task<Result<MemberProfileDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The calling member edits their own contact fields.</summary>
    Task<Result<MemberProfileDto>> UpdateMineAsync(
        Guid userId, UpdateMyMemberProfileRequest request, CancellationToken cancellationToken);

    /// <summary>The Choir Director's member list with approved skills, the basis for assigning members to songs.</summary>
    Task<Result<PagedList<MemberProfileSummaryDto>>> SearchAsync(
        SearchMemberProfilesRequest request, CancellationToken cancellationToken);

    Task<Result<MemberProfileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The Choir Director edits any member's profile, including joined date and status.</summary>
    Task<Result<MemberProfileDto>> UpdateAsync(
        Guid id, UpdateMemberProfileRequest request, CancellationToken cancellationToken);
}

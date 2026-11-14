using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IMemberProfileService
{
    /// <summary>
    /// Profile of the calling user with system role and approved skills; fails with MEMBER_NOT_FOUND if the
    /// account has none.
    /// </summary>
    Task<Result<MemberProfileDetailDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The calling member edits their own contact fields.</summary>
    Task<Result<MemberProfileDto>> UpdateMineAsync(
        Guid userId, UpdateMyMemberProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The Choir Director's member list with approved skills, the basis for assigning members to songs. Covers the
    /// whole parish, so a director can find members of other choirs to add to their own.
    /// </summary>
    Task<Result<PagedList<MemberProfileSummaryDto>>> SearchAsync(
        SearchMemberProfilesRequest request, CancellationToken cancellationToken);

    /// <summary>A member the Choir Director manages (D8); anyone else fails with MEMBER_NOT_FOUND.</summary>
    Task<Result<MemberProfileDto>> GetByIdAsync(Guid directorUserId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The Choir Director edits the profile of a member they manage (D8), including joined date and status;
    /// anyone else fails with MEMBER_NOT_FOUND.
    /// </summary>
    Task<Result<MemberProfileDto>> UpdateAsync(
        Guid directorUserId, Guid id, UpdateMemberProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The calling member's past Published events they were involved in, newest first, with their participation,
    /// served skills, rehearsal attendance and practice results (UC-11 / FE-14). Fails with MEMBER_NOT_FOUND if
    /// the account has no member profile.
    /// </summary>
    Task<Result<PagedList<ParticipationHistoryDto>>> GetMyHistoryAsync(
        Guid userId, SearchMyParticipationHistoryRequest request, CancellationToken cancellationToken);
}

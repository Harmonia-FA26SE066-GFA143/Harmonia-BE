using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Moves of members into choirs that wait for the other side (2026-11-18). A Choir Member asks to join or transfer;
/// the choir's director or the Admin answers. A director invites a member with no choir (through
/// POST api/choirs/{id}/members); the member answers. Rows outside the caller's scope read as not found.
/// </summary>
public interface IChoirJoinRequestService
{
    /// <summary>The calling member asks to join the choir, or to transfer to it; one own request Pending at a time.</summary>
    Task<Result<ChoirJoinRequestDto>> CreateAsync(CreateChoirJoinRequestRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Newest first. A member sees their own rows, a director the rows of the choirs they direct, the Admin every row.
    /// </summary>
    Task<Result<PagedList<ChoirJoinRequestDto>>> SearchAsync(
        SearchChoirJoinRequestsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The answering side accepts: the member leaves their current choir and joins this one in one save, and every
    /// other Pending row of the member is cancelled.
    /// </summary>
    Task<Result<ChoirJoinRequestDto>> ApproveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The answering side declines; a director rejecting a member's request must give a reason.</summary>
    Task<Result<ChoirJoinRequestDto>> RejectAsync(
        Guid id, RejectChoirJoinRequestRequest request, CancellationToken cancellationToken);

    /// <summary>The creating side withdraws it while Pending.</summary>
    Task<Result<ChoirJoinRequestDto>> CancelAsync(Guid id, CancellationToken cancellationToken);
}

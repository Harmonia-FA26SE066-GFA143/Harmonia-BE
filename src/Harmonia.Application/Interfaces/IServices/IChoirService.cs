using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Choirs of the parish, their members and the events they serve (D7). The Admin creates choirs and assigns each one
/// Choir Director; a director manages only their own choirs, and another director's choir reads as not found.
/// </summary>
public interface IChoirService
{
    Task<Result<ChoirDto>> CreateAsync(CreateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Every choir for the Admin and the Parish Priest, only the caller's own choirs for a Choir Director, the active
    /// choirs for a Choir Member; ordered by name.
    /// </summary>
    Task<Result<List<ChoirDto>>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Active choirs by name, for the anonymous join form.</summary>
    Task<Result<List<ChoirSummaryDto>>> GetPublicAsync(CancellationToken cancellationToken);

    Task<Result<ChoirDto>> UpdateAsync(Guid id, UpdateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>Active members of the choir, ordered by name.</summary>
    Task<Result<List<ChoirMemberDto>>> GetMembersAsync(Guid choirId, CancellationToken cancellationToken);

    /// <summary>
    /// The Admin places the members in the choir directly and their Pending join requests close; a Choir Director
    /// invites them instead, and each joins once they accept. A member in another choir is refused.
    /// </summary>
    Task<Result> AddMembersAsync(Guid choirId, AddChoirMembersRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the member into the choir, leaving their current choir (with its upcoming-event clean-up) in the same
    /// save. That save also carries whatever the caller staged on the request's unit of work, e.g. the join request
    /// status. The old choir's director is notified.
    /// </summary>
    Task<Result> MoveMemberAsync(Guid memberId, Guid choirId, CancellationToken cancellationToken);

    /// <summary>Marks the membership as left; the row stays as history.</summary>
    Task<Result> RemoveMemberAsync(Guid choirId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>The calling Choir Member leaves the choir on their own; the choir's director is notified.</summary>
    Task<Result> LeaveAsync(Guid choirId, CancellationToken cancellationToken);

    /// <summary>
    /// For a member who stops serving (status no longer Active, or a role other than Choir Member): deletes their
    /// invitations and roster lines on upcoming events, reopening a finalized roster, saves every pending change and
    /// tells each event's lead director why (<paramref name="reason"/>, Vietnamese, e.g. "chuyển sang tạm nghỉ").
    /// </summary>
    Task DropMemberFromUpcomingEventsAsync(
        Guid memberId, string? memberFullName, string reason, CancellationToken cancellationToken);

    /// <summary>Choirs serving the event, the lead choir first, then by name.</summary>
    Task<Result<List<LiturgicalEventChoirDto>>> GetEventChoirsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// The Parish Priest replaces the choirs serving the event and picks the lead choir (D8). Cancelled and past
    /// events are locked. Members left in no serving choir lose their invitation and roster lines (a finalized roster
    /// reopens), the event's song lists move to a new lead, and the affected directors and members are notified.
    /// </summary>
    Task<Result<List<LiturgicalEventChoirDto>>> UpdateEventChoirsAsync(
        Guid eventId, UpdateEventChoirsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The Parish Priest sets or clears another Choir Director to stand in for the choir's own director at this event
    /// only, e.g. when the director is suddenly absent. Cancelled and past events are locked.
    /// </summary>
    Task<Result<List<LiturgicalEventChoirDto>>> UpdateSubstituteDirectorAsync(
        Guid eventId, Guid choirId, UpdateSubstituteDirectorRequest request, CancellationToken cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Service roster of an event (UC-25 / FE-36).</summary>
public interface IRosterService
{
    /// <summary>
    /// Suggests who serves each song / skill of the event's approved song list, from members with the
    /// approved skill who confirmed participation. Replaces the previous suggested assignments, keeps the
    /// manual ones and counts them as filled. Uses AI when available and rules otherwise; slots that still
    /// lack people come back as shortages.
    /// </summary>
    Task<Result<RosterSuggestionResponse>> SuggestAsync(SuggestServiceRosterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Song / skill requirements of the event's approved song list that the current roster does not fully
    /// staff (UC-25a / FE-37). Every existing assignment counts as filled; with no roster yet, every
    /// requirement is a shortage.
    /// </summary>
    Task<Result<List<RosterShortageDto>>> GetShortagesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Current roster of the event with its Active assignments and shortages, so a saved roster can be reopened.
    /// Shortages are empty when the event no longer has an approved song list.
    /// </summary>
    Task<Result<ServiceRosterDto>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Manually assigns a member to a song / skill requirement of the event's approved song list (UC-25b / FE-38).
    /// The member needs the approved skill and a confirmed participation; the requirement's count may be exceeded.
    /// Creates a Draft roster when the event has none yet.
    /// </summary>
    Task<Result<RosterAssignmentDto>> AddAssignmentAsync(CreateRosterAssignmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the member of an Active assignment (UC-25b / FE-38): the old line is kept as Replaced and linked
    /// to a new Manual line for the same song and skill. Returns the new line.
    /// </summary>
    Task<Result<RosterAssignmentDto>> ReplaceAssignmentAsync(
        Guid assignmentId, ReplaceRosterAssignmentRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes an Active assignment from a roster that is not finalized (UC-25b / FE-38).</summary>
    Task<Result> RemoveAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the roster once the event's song list is approved (UC-26 / FE-39). Every Active assignment must still
    /// hold an Active member with the approved skill and a confirmed participation; shortages do not block and come
    /// back in the result.
    /// </summary>
    Task<Result<ServiceRosterDto>> FinalizeAsync(Guid rosterId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends each selected member one notification listing their lines on the finalized roster and stamps
    /// those lines' NotifiedAt (UC-27 / FE-40).
    /// </summary>
    Task<Result> SendNotificationsAsync(
        Guid rosterId, SendRosterNotificationsRequest request, CancellationToken cancellationToken);
}

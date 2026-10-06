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
}

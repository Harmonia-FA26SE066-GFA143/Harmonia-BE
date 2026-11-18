using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>People an event needs for the whole service (organist, guitarist, conductor), not per song.</summary>
public interface IEventPersonnelRequirementService
{
    /// <summary>Requirements of the event, ordered by skill name; an event outside the caller's scope is LITURGICAL_EVENT_NOT_FOUND.</summary>
    Task<Result<List<EventPersonnelRequirementDto>>> GetAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the event's whole requirement set. Only the lead choir's director may, not once the event is cancelled
    /// or past, nor once its roster is finalized. Only newly added skills must be active. Does not need a song list.
    /// </summary>
    Task<Result<List<EventPersonnelRequirementDto>>> UpdateAsync(
        Guid eventId, UpdateEventPersonnelRequirementsRequest request, CancellationToken cancellationToken);
}

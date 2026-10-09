using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Readiness of the choir for an event: per member for the Choir Director (UC-30 / FE-46),
/// as a choir-wide summary for the Parish Priest (UC-15 / FE-21).
/// </summary>
public interface IEventPreparationService
{
    /// <summary>
    /// Participation, roster and practice totals for the event. Attendance and practice sum the rows of
    /// <see cref="GetProgressAsync"/>. A missing event fails with EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<EventPreparationStatusDto>> GetStatusAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// One row per active member, ordered by name. Any event can be read, past ones included.
    /// A missing event fails with EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<List<EventPreparationProgressDto>>> GetProgressAsync(Guid eventId, CancellationToken cancellationToken);
}

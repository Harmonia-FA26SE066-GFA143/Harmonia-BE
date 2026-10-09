using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>The Choir Director tracks practice progress and rehearsal attendance ahead of an event (UC-30 / FE-46).</summary>
public interface IEventPreparationService
{
    /// <summary>
    /// One row per active member, ordered by name. Any event can be read, past ones included.
    /// A missing event fails with EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<List<EventPreparationProgressDto>>> GetProgressAsync(Guid eventId, CancellationToken cancellationToken);
}

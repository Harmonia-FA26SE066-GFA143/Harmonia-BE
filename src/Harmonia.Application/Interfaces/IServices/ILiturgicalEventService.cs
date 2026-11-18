using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ILiturgicalEventService
{
    /// <summary>
    /// Events past and upcoming for the priest's program pages (UC-12 / FE-15) and the director's event list (B23).
    /// Draft events only when <paramref name="includeDrafts"/>: they stay the priest's until published.
    /// </summary>
    Task<Result<PagedList<LiturgicalEventListItemDto>>> SearchAsync(
        SearchLiturgicalEventsRequest request, bool includeDrafts, CancellationToken cancellationToken);

    Task<Result<LiturgicalEventDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<LiturgicalEventDto>> CreateAsync(
        CreateLiturgicalEventRequest request, CancellationToken cancellationToken);

    /// <summary>Edits a Draft or Published event (UC-12 / FE-16); a cancelled event can no longer change.</summary>
    Task<Result<LiturgicalEventDto>> UpdateAsync(
        Guid id, UpdateLiturgicalEventRequest request, CancellationToken cancellationToken);

    Task<Result<LiturgicalEventDto>> PublishAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels an event that has not passed. When it was published, every active Choir Director and member
    /// is notified (S-05).
    /// </summary>
    Task<Result<LiturgicalEventDto>> CancelAsync(Guid id, CancellationToken cancellationToken);
}

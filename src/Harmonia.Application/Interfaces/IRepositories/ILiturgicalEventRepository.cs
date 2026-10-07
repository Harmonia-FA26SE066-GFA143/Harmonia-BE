using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILiturgicalEventRepository : IGenericRepository<LiturgicalEvent>
{
    /// <summary>True when another event already uses the date + time + location; <paramref name="excludeId"/> skips the event being edited.</summary>
    Task<bool> ExistsBySlotAsync(
        DateOnly eventDate, TimeOnly time, Guid locationId, Guid? excludeId, CancellationToken cancellationToken);

    Task<List<LiturgicalEvent>> GetUpcomingPublishedAsync(DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>Read-only page of events of every status, with Location loaded, ordered by date then time.</summary>
    Task<PagedList<LiturgicalEvent>> SearchAsync(SearchLiturgicalEventsRequest filter, CancellationToken cancellationToken);

    /// <summary>Tracked event with its Location loaded.</summary>
    Task<LiturgicalEvent?> GetWithLocationAsync(Guid id, CancellationToken cancellationToken);
}

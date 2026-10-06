using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILiturgicalEventRepository : IGenericRepository<LiturgicalEvent>
{
    Task<bool> ExistsBySlotAsync(
        DateOnly eventDate, TimeOnly time, Guid locationId, CancellationToken cancellationToken);

    Task<List<LiturgicalEvent>> GetUpcomingPublishedAsync(DateOnly fromDate, CancellationToken cancellationToken);
}
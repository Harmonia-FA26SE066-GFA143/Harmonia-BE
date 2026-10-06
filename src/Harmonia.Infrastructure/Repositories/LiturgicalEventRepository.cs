using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Infrastructure.Repositories;

public class LiturgicalEventRepository(HarmoniaDbContext dbContext)
    : GenericRepository<LiturgicalEvent>(dbContext), ILiturgicalEventRepository
{
    public Task<bool> ExistsBySlotAsync(
        DateOnly eventDate, TimeOnly time, Guid locationId, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents.AnyAsync(
            x => x.EventDate == eventDate && x.Time == time && x.LocationId == locationId,
            cancellationToken);

    public Task<List<LiturgicalEvent>> GetUpcomingPublishedAsync(
        DateOnly fromDate, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents
            .Include(x => x.Location)
            .Where(x => x.EventDate >= fromDate && x.Status == EventStatus.Published)
            .OrderBy(x => x.EventDate)
            .ThenBy(x => x.Time)
            .ToListAsync(cancellationToken);
}
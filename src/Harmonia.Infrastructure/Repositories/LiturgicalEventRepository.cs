using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
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
        DateOnly eventDate, TimeOnly time, Guid locationId, Guid? excludeId, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents.AnyAsync(
            x => x.EventDate == eventDate && x.Time == time && x.LocationId == locationId && x.Id != excludeId,
            cancellationToken);

    public Task<List<LiturgicalEvent>> GetUpcomingPublishedAsync(
        DateOnly fromDate, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents
            .Include(x => x.Location)
            .Where(x => x.EventDate >= fromDate && x.Status == EventStatus.Published)
            .OrderBy(x => x.EventDate)
            .ThenBy(x => x.Time)
            .ToListAsync(cancellationToken);

    public async Task<PagedList<LiturgicalEvent>> SearchAsync(
        SearchLiturgicalEventsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.LiturgicalEvents.AsNoTracking();

        if (filter.FromDate is { } fromDate)
        {
            query = query.Where(x => x.EventDate >= fromDate);
        }

        if (filter.ToDate is { } toDate)
        {
            query = query.Where(x => x.EventDate <= toDate);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Location)
            .OrderBy(x => x.EventDate)
            .ThenBy(x => x.Time)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<LiturgicalEvent>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<LiturgicalEvent?> GetWithLocationAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<LiturgicalEvent?> GetWithPreparationAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.LiturgicalEvents
            .AsNoTracking()
            .Include(x => x.SongLists)
            .Include(x => x.EventParticipations)
            .Include(x => x.Rehearsals)
            .Include(x => x.ServiceRoster).ThenInclude(x => x!.Assignments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Infrastructure.Repositories;

public class RehearsalRepository(HarmoniaDbContext dbContext)
    : GenericRepository<Rehearsal>(dbContext), IRehearsalRepository
{
    public Task<List<Rehearsal>> GetUpcomingAsync(DateTime fromTime, CancellationToken cancellationToken) =>
        DbContext.Rehearsals
            .Include(x => x.Location)
            .Include(x => x.Songs).ThenInclude(s => s.Song)
            .Where(x => x.EndTime >= fromTime)
            .OrderBy(x => x.StartTime)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<PagedList<Rehearsal>> SearchAsync(
        SearchRehearsalsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.Rehearsals.AsNoTracking();

        if (filter.EventId is { } eventId)
        {
            query = query.Where(x => x.EventId == eventId);
        }

        // Dates are parish dates; StartTime is stored in UTC.
        if (filter.FromDate is { } fromDate)
        {
            var from = VietnamTime.StartOfDayUtc(fromDate);
            query = query.Where(x => x.StartTime >= from);
        }

        if (filter.ToDate is { } toDate)
        {
            var before = VietnamTime.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(x => x.StartTime < before);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Location)
            .Include(x => x.Songs).ThenInclude(s => s.Song)
            .AsSplitQuery()
            .OrderByDescending(x => x.StartTime)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Rehearsal>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<Rehearsal?> GetWithDetailsForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.Rehearsals
            .Include(x => x.Location)
            .Include(x => x.Songs).ThenInclude(s => s.Song)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Rehearsal?> GetWithSongsForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.Rehearsals
            .Include(x => x.Songs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<RehearsalSong>> GetSongsAsync(Guid rehearsalId, CancellationToken cancellationToken) =>
        DbContext.RehearsalSongs
            .AsNoTracking()
            .Include(x => x.Song)
            .Where(x => x.RehearsalId == rehearsalId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

    public Task<List<Rehearsal>> GetByEventWithAttendancesAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.Rehearsals
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Include(x => x.Attendances)
            .ToListAsync(cancellationToken);

    public Task<Rehearsal?> GetWithAttendancesForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.Rehearsals
            .Include(x => x.Attendances)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> TrySaveAttendancesAsync(Rehearsal rehearsal, CancellationToken cancellationToken)
    {
        DbContext.ChangeTracker.DetectChanges();
        var added = rehearsal.Attendances.Where(a => DbContext.Entry(a).State == EntityState.Added).ToList();

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (added.Count > 0)
        {
            // Only a row another request inserted for one of our new members means the unique
            // (RehearsalId, MemberId) index fired; checking for it keeps this provider-neutral.
            var addedMemberIds = added.Select(a => a.MemberId).ToList();
            if (await DbContext.RehearsalAttendances.AsNoTracking().AnyAsync(
                    x => x.RehearsalId == rehearsal.Id && addedMemberIds.Contains(x.MemberId), cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    public Task<bool> HasConflictAsync(
        Guid locationId, DateTime start, DateTime end, Guid? excludeId, CancellationToken cancellationToken) =>
        DbContext.Rehearsals.AsNoTracking().AnyAsync(
            x => x.LocationId == locationId && x.StartTime < end && start < x.EndTime && x.Id != excludeId,
            cancellationToken);
}
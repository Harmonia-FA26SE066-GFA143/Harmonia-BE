using Harmonia.Application.Interfaces.IRepositories;
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
            .Where(x => x.StartTime >= fromTime)
            .OrderBy(x => x.StartTime)
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
}
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
}
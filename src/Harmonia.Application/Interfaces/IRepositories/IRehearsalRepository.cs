using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IRehearsalRepository : IGenericRepository<Rehearsal>
{
    Task<List<Rehearsal>> GetUpcomingAsync(DateTime fromTime, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="Rehearsal.Attendances"/> loaded.</summary>
    Task<Rehearsal?> GetWithAttendancesForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves pending attendance changes. Returns false when another request recorded the same member
    /// first (unique (RehearsalId, MemberId)); nothing is saved then.
    /// </summary>
    Task<bool> TrySaveAttendancesAsync(Rehearsal rehearsal, CancellationToken cancellationToken);
}
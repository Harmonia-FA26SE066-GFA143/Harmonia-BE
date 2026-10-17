using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IEventParticipationRepository : IGenericRepository<EventParticipation>
{
    Task<EventParticipation?> GetByEventAndMemberAsync(Guid eventId, Guid memberId, CancellationToken cancellationToken);

    Task<List<EventParticipation>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only requests of the member for Published events dated from <paramref name="fromDate"/>, soonest first,
    /// with LiturgicalEvent loaded.
    /// </summary>
    Task<List<EventParticipation>> GetUpcomingForMemberAsync(
        Guid memberId, DateOnly fromDate, CancellationToken cancellationToken);

    Task<List<Guid>> GetRequestedMemberIdsAsync(Guid eventId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken);
}
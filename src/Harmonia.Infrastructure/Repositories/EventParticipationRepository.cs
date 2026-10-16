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

public class EventParticipationRepository(HarmoniaDbContext dbContext)
    : GenericRepository<EventParticipation>(dbContext), IEventParticipationRepository
{
    public Task<EventParticipation?> GetByEventAndMemberAsync(
        Guid eventId, Guid memberId, CancellationToken cancellationToken) =>
        DbContext.EventParticipations
            .Include(x => x.LiturgicalEvent)
            .Include(x => x.Member.User)
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.MemberId == memberId, cancellationToken);

    public Task<List<EventParticipation>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.EventParticipations
            .AsNoTracking()
            .Include(x => x.Member.User)
            .Where(x => x.EventId == eventId)
            .OrderBy(x => x.Member.User.FullName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<List<EventParticipation>> GetUpcomingForMemberAsync(
        Guid memberId, DateOnly fromDate, CancellationToken cancellationToken) =>
        DbContext.EventParticipations
            .AsNoTracking()
            .Include(x => x.LiturgicalEvent)
            .Where(x => x.MemberId == memberId
                && x.LiturgicalEvent.Status == EventStatus.Published
                && x.LiturgicalEvent.EventDate >= fromDate)
            .OrderBy(x => x.LiturgicalEvent.EventDate)
            .ThenBy(x => x.LiturgicalEvent.Time)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<List<Guid>> GetRequestedMemberIdsAsync(
        Guid eventId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken) =>
        DbContext.EventParticipations
            .AsNoTracking()
            .Where(x => x.EventId == eventId && memberIds.Contains(x.MemberId))
            .Select(x => x.MemberId)
            .ToListAsync(cancellationToken);
}
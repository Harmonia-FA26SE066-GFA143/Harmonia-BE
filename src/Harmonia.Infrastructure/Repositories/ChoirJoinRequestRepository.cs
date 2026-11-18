using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class ChoirJoinRequestRepository(HarmoniaDbContext dbContext)
    : GenericRepository<ChoirJoinRequest>(dbContext), IChoirJoinRequestRepository
{
    public Task<ChoirJoinRequest?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        WithDetails().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedList<ChoirJoinRequest>> SearchAsync(
        SearchChoirJoinRequestsRequest filter, Guid? memberId, Guid? directorUserId, CancellationToken cancellationToken)
    {
        var query = WithDetails();

        if (memberId is not null)
        {
            query = query.Where(x => x.MemberId == memberId);
        }

        if (directorUserId is not null)
        {
            query = query.Where(x => x.Choir.DirectorId == directorUserId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (filter.ChoirId is { } choirId)
        {
            query = query.Where(x => x.ChoirId == choirId);
        }

        if (filter.IsInvitation is { } isInvitation)
        {
            query = query.Where(x => x.IsInvitation == isInvitation);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<ChoirJoinRequest>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<List<ChoirJoinRequest>> GetPendingForUpdateAsync(
        IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken) =>
        DbContext.ChoirJoinRequests
            .Where(x => memberIds.Contains(x.MemberId) && x.Status == ChoirJoinRequestStatus.Pending)
            .ToListAsync(cancellationToken);

    private IQueryable<ChoirJoinRequest> WithDetails() =>
        DbContext.ChoirJoinRequests
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Member).ThenInclude(x => x.User)
            .Include(x => x.Member).ThenInclude(x => x.ChoirMemberships.Where(m => m.IsActive)).ThenInclude(x => x.Choir)
            .Include(x => x.Choir);
}

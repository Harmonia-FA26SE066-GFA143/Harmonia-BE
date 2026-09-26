using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class NotificationRepository(HarmoniaDbContext dbContext)
    : GenericRepository<Notification>(dbContext), INotificationRepository
{
    public async Task<PagedList<NotificationRecipient>> GetForUserAsync(
        Guid userId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.NotificationRecipients
            .AsNoTracking()
            .Include(x => x.Notification)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Notification.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<NotificationRecipient>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public Task<NotificationRecipient?> GetRecipientAsync(
        Guid notificationId, Guid userId, CancellationToken cancellationToken) =>
        DbContext.NotificationRecipients
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        DbContext.NotificationRecipients
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);
}

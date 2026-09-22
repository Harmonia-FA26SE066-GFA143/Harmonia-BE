using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class NotificationRepository(HarmoniaDbContext dbContext) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AddAsync(notification, cancellationToken);

    public async Task<PagedList<NotificationRecipient>> GetForUserAsync(
        Guid userId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = dbContext.NotificationRecipients
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
        dbContext.NotificationRecipients
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId, cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationRecipients
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

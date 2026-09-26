using Harmonia.Application.Common.Models;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>One page of the caller's own notifications, newest first, with the notification loaded.</summary>
    Task<PagedList<NotificationRecipient>> GetForUserAsync(
        Guid userId, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>Scoped by user id: another user's row comes back as null, never as someone else's data.</summary>
    Task<NotificationRecipient?> GetRecipientAsync(
        Guid notificationId, Guid userId, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);
}

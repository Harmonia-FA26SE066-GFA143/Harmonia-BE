using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface INotificationService
{
    /// <summary>Stores the notification and its recipient rows, then pushes it to whoever is connected.</summary>
    Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken);

    /// <summary>The caller's notifications, newest first; a null <paramref name="isRead"/> returns every one.</summary>
    Task<Result<PagedList<NotificationDto>>> GetForUserAsync(
        Guid userId, bool? isRead, PagingRequest paging, CancellationToken cancellationToken);

    Task<Result<int>> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Marks every unread notification of the caller as read; with none unread it is a no-op.</summary>
    Task<Result> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken);
}

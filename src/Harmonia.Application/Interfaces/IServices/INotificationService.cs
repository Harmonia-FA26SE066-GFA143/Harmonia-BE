using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface INotificationService
{
    /// <summary>Stores the notification and its recipient rows, then pushes it to whoever is connected.</summary>
    Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken);

    Task<Result<PagedList<NotificationDto>>> GetForUserAsync(
        Guid userId, PagingRequest paging, CancellationToken cancellationToken);

    Task<Result<int>> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
}

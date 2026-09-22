using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Real-time delivery to connected clients. Implemented in the API layer, which is the only
/// layer that may touch SignalR; business services depend on this instead of on IHubContext.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(
        NotificationDto notification, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

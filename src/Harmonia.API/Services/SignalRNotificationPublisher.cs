using Harmonia.API.Hubs;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Harmonia.API.Services;

public class SignalRNotificationPublisher(
    IHubContext<NotificationHub, INotificationClient> hubContext,
    ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    /// <summary>
    /// Best effort: the notification is already stored, so a client that misses the push still sees it on its
    /// next fetch. A failed push is logged and swallowed, never failing the business operation that sent it.
    /// </summary>
    public async Task PublishAsync(
        NotificationDto notification, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .Users(userIds.Select(id => id.ToString()).ToList())
                .ReceiveNotificationAsync(notification, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push of a {NotificationType} notification to {RecipientCount} recipient(s) failed",
                notification.Type, userIds.Count);
        }
    }
}

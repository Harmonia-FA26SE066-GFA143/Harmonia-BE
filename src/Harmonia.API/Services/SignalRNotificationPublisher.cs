using Harmonia.API.Hubs;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Harmonia.API.Services;

public class SignalRNotificationPublisher(IHubContext<NotificationHub, INotificationClient> hubContext)
    : INotificationPublisher
{
    public Task PublishAsync(
        NotificationDto notification, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Users(userIds.Select(id => id.ToString()).ToList())
            .ReceiveNotificationAsync(notification, cancellationToken);
}

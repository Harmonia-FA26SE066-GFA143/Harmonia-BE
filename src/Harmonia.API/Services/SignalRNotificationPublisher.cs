using Harmonia.API.Hubs;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.SignalR;

namespace Harmonia.API.Services;

public class SignalRNotificationPublisher(
    IHubContext<NotificationHub, INotificationClient> hubContext,
    ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    public Task PublishAsync(
        NotificationDto notification, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        TryPushAsync(client => client.ReceiveNotificationAsync(notification, cancellationToken),
            $"{notification.Type} notification", userIds);

    public Task PublishDirectorNoteAsync(
        DirectorNoteDto note, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        TryPushAsync(client => client.ReceiveDirectorNoteAsync(note, cancellationToken), "director note", userIds);

    public Task PublishDirectorNotesReadAsync(
        DirectorNotesReadDto read, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        TryPushAsync(client => client.ReceiveDirectorNotesReadAsync(read, cancellationToken), "director notes read", userIds);

    /// <summary>
    /// Best effort: the data is already stored, so a client that misses the push still sees it on its
    /// next fetch. A failed push is logged and swallowed, never failing the business operation that sent it.
    /// </summary>
    private async Task TryPushAsync(
        Func<INotificationClient, Task> push, string what, IReadOnlyCollection<Guid> userIds)
    {
        try
        {
            await push(hubContext.Clients.Users(userIds.Distinct().Select(id => id.ToString()).ToList()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime push of a {What} to {RecipientCount} recipient(s) failed", what, userIds.Count);
        }
    }
}

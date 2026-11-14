using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Real-time delivery to connected clients. Implemented in the API layer, which is the only
/// layer that may touch SignalR; business services depend on this instead of on IHubContext.
/// Every method is best effort: the data is already stored, so a missed push is caught up on the next fetch.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(
        NotificationDto notification, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task PublishDirectorNoteAsync(
        DirectorNoteDto note, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    Task PublishDirectorNotesReadAsync(
        DirectorNotesReadDto read, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

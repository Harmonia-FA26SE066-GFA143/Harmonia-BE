using Harmonia.Application.DTOs;

namespace Harmonia.API.Hubs;

/// <summary>
/// The methods the server calls on a connected client. Each name is also the wire message
/// name the client subscribes to, so renaming one breaks every subscribed client.
/// A trailing CancellationToken is stripped by SignalR and handed to the transport rather
/// than serialised, so it never reaches the client as an argument.
/// </summary>
public interface INotificationClient
{
    Task ReceiveNotificationAsync(NotificationDto notification, CancellationToken cancellationToken);

    /// <summary>A new note or chat message between the Parish Priest and a Choir Director.</summary>
    Task ReceiveDirectorNoteAsync(DirectorNoteDto note, CancellationToken cancellationToken);

    /// <summary>One side of a conversation read every note the other side sent them.</summary>
    Task ReceiveDirectorNotesReadAsync(DirectorNotesReadDto read, CancellationToken cancellationToken);
}

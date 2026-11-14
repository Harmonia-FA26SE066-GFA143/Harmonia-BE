using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Notes and chat between the Parish Priest and Choir Directors (UC-17 / FE-23). Every new note is pushed in
/// real time to both parties.
/// </summary>
public interface IDirectorNoteService
{
    /// <summary>
    /// Who the caller can write to, ordered by name: active Choir Directors for the Parish Priest,
    /// the active Parish Priest for a Choir Director.
    /// </summary>
    Task<Result<List<UserSummaryDto>>> GetRecipientsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores one copy of the note per recipient and notifies each of them (S-05). Every recipient must be an
    /// active Choir Director, otherwise DIRECTOR_NOTE_RECIPIENT_INVALID; a missing event fails with LITURGICAL_EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<List<DirectorNoteDto>>> CreateAsync(
        Guid fromUserId, CreateDirectorNoteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// A chat message, Parish Priest to Choir Director or back; any other pair fails with DIRECTOR_NOTE_RECIPIENT_INVALID.
    /// Pushed in real time only, no Notification is stored.
    /// </summary>
    Task<Result<DirectorNoteDto>> SendMessageAsync(
        Guid fromUserId, SendDirectorNoteMessageRequest request, CancellationToken cancellationToken);

    /// <summary>Notes the caller sent or received, newest first.</summary>
    Task<Result<PagedList<DirectorNoteDto>>> SearchAsync(
        Guid userId, SearchDirectorNotesRequest request, CancellationToken cancellationToken);

    /// <summary>Everyone the caller has exchanged notes with, latest conversation first.</summary>
    Task<Result<List<DirectorNoteConversationDto>>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks read every note <paramref name="otherUserId"/> sent the caller and tells both parties.</summary>
    Task<Result> MarkConversationReadAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    /// <summary>A note the caller sent or received; anyone else's fails with DIRECTOR_NOTE_NOT_FOUND.</summary>
    Task<Result<DirectorNoteDto>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Notes and requests from the Parish Priest to Choir Directors (UC-17 / FE-23).</summary>
public interface IDirectorNoteService
{
    /// <summary>Active Choir Directors, ordered by name: who the Parish Priest can address a note to.</summary>
    Task<Result<List<UserSummaryDto>>> GetRecipientsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores one copy of the note per recipient and notifies each of them (S-05). Every recipient must be an
    /// active Choir Director, otherwise DIRECTOR_NOTE_RECIPIENT_INVALID; a missing event fails with EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<List<DirectorNoteDto>>> CreateAsync(
        Guid fromUserId, CreateDirectorNoteRequest request, CancellationToken cancellationToken);

    /// <summary>Notes the caller sent or received, newest first.</summary>
    Task<Result<PagedList<DirectorNoteDto>>> SearchAsync(
        Guid userId, SearchDirectorNotesRequest request, CancellationToken cancellationToken);

    /// <summary>A note the caller sent or received; anyone else's fails with DIRECTOR_NOTE_NOT_FOUND.</summary>
    Task<Result<DirectorNoteDto>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

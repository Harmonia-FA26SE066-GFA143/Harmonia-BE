using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IDirectorNoteRepository : IGenericRepository<DirectorNote>
{
    /// <summary>
    /// Read-only page of notes the user sent or received, newest first, narrowed to the event and to the other
    /// party of <paramref name="filter"/> when set. LiturgicalEvent, FromUser and ToUser are loaded.
    /// </summary>
    Task<PagedList<DirectorNote>> SearchForUserAsync(
        Guid userId, SearchDirectorNotesRequest filter, CancellationToken cancellationToken);

    /// <summary>Read-only, with LiturgicalEvent, FromUser and ToUser loaded; null when missing.</summary>
    Task<DirectorNote?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// One entry per person the user has exchanged notes with: the latest note (details loaded) and how many notes
    /// that person sent the user unread. Newest conversation first.
    /// </summary>
    Task<List<(DirectorNote LastNote, int UnreadCount)>> GetConversationsAsync(
        Guid userId, CancellationToken cancellationToken);

    /// <summary>Sets ReadAt on every unread note <paramref name="fromUserId"/> sent <paramref name="toUserId"/>; returns how many changed.</summary>
    Task<int> MarkReadAsync(Guid toUserId, Guid fromUserId, DateTime readAt, CancellationToken cancellationToken);
}

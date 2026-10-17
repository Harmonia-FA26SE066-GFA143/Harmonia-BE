using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IDirectorNoteRepository : IGenericRepository<DirectorNote>
{
    /// <summary>
    /// Read-only page of notes the user sent or received, newest first, narrowed to the event of
    /// <paramref name="filter"/> when set. LiturgicalEvent, FromUser and ToUser are loaded.
    /// </summary>
    Task<PagedList<DirectorNote>> SearchForUserAsync(
        Guid userId, SearchDirectorNotesRequest filter, CancellationToken cancellationToken);

    /// <summary>Read-only, with LiturgicalEvent, FromUser and ToUser loaded; null when missing.</summary>
    Task<DirectorNote?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Song library (UC-21 / FE-27, FE-29). Deleted songs behave as SONG_NOT_FOUND everywhere.</summary>
public interface ISongService
{
    Task<Result<PagedList<SongDto>>> SearchAsync(SearchSongsRequest request, CancellationToken cancellationToken);

    Task<Result<SongDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<SongDto>> CreateAsync(CreateSongRequest request, CancellationToken cancellationToken);

    Task<Result<SongDto>> UpdateAsync(Guid id, UpdateSongRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive to false so song lists and assignments keep their history. Not reversible.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<SongClassificationDto>> GetClassificationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the song's whole classification with the request. Only newly added lookups and skills
    /// must be active; ones the song already had are kept even if deactivated since.
    /// </summary>
    Task<Result<SongClassificationDto>> UpdateClassificationAsync(
        Guid id, UpdateSongClassificationRequest request, CancellationToken cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Music materials of a song (UC-21 / FE-28). Files are private and read through signed URLs.</summary>
public interface IMusicMaterialService
{
    /// <summary>
    /// Checks the file against the whitelist of the material type and the size limit, stores it
    /// privately, then saves the row. <paramref name="file"/> null means no file was sent.
    /// </summary>
    Task<Result<MusicMaterialDto>> UploadAsync(
        UploadMusicMaterialRequest request, FileContent? file, CancellationToken cancellationToken);

    Task<Result<PagedList<MusicMaterialDto>>> GetBySongAsync(
        Guid songId, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Materials the calling member can use (UC-07 / FE-08): those for everyone plus those for the member's
    /// approved skills. <paramref name="songId"/> narrows the list to one song.
    /// </summary>
    Task<Result<PagedList<MusicMaterialDto>>> GetMineAsync(
        Guid userId, Guid? songId, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>Changes title and target skill only; the file, type and id stay, so learning progress is kept.</summary>
    Task<Result<MusicMaterialDto>> UpdateAsync(Guid id, UpdateMusicMaterialRequest request, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive to false and keeps the stored file, which learning progress still points at.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

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
    /// approved skills, each with the member's own learning status. The filters of <paramref name="request"/>
    /// narrow the list (UC-07E).
    /// </summary>
    Task<Result<PagedList<MusicMaterialDetailDto>>> GetMineAsync(
        Guid userId, SearchMusicMaterialsRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Learning status of every active member expected to learn the material, for the Choir Director (UC-08 / FE-09),
    /// limited to the members that director manages (D8). Members who never marked it show as NotStarted.
    /// </summary>
    Task<Result<PagedList<MaterialLearningProgressDetailDto>>> GetLearningProgressAsync(
        Guid directorUserId, Guid materialId, SearchMaterialLearningProgressRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a material as learned or as needing practice for the calling member (UC-08 / FE-09).
    /// Creates the progress row on first use. A material the member cannot see returns MATERIAL_NOT_FOUND.
    /// </summary>
    Task<Result<MaterialLearningProgressDto>> UpdateLearningProgressAsync(
        Guid userId, Guid materialId, UpdateMaterialLearningProgressRequest request, CancellationToken cancellationToken);

    /// <summary>Changes title and target skill only; the file, type and id stay, so learning progress is kept.</summary>
    Task<Result<MusicMaterialDto>> UpdateAsync(Guid id, UpdateMusicMaterialRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Swaps the stored file of an active material under the same type rules as an upload; the id, title, type,
    /// target skill and practice assignments stay. Members' learning progress is kept unless
    /// <see cref="ReplaceMusicMaterialFileRequest.ResetProgress"/> is set. The old file is removed from storage.
    /// MATERIAL_NOT_FOUND, MATERIAL_FILE_REQUIRED, MATERIAL_FILE_TYPE_NOT_ALLOWED, MATERIAL_FILE_TOO_LARGE,
    /// EXTERNAL_STORAGE_FAILED.
    /// </summary>
    Task<Result<MusicMaterialDto>> ReplaceFileAsync(
        Guid id, ReplaceMusicMaterialFileRequest request, FileContent? file, CancellationToken cancellationToken);

    /// <summary>
    /// Fresh signed URLs of the rendering of a MIDI material, for the mobile piano roll.
    /// Rendering happens on upload or file replacement, so nothing is rendered here.
    /// MATERIAL_NOT_FOUND, MATERIAL_PLAYBACK_UNAVAILABLE.
    /// </summary>
    Task<Result<PlaybackDto>> GetPlaybackAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Soft delete: sets IsActive to false and keeps the stored file, which learning progress still points at.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

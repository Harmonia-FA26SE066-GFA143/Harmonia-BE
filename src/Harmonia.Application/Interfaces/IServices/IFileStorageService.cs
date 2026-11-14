using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Stores uploaded files outside the database. The returned <see cref="FileUploadResponse.PublicId"/>
/// includes the file extension (e.g. "harmonia/practice-submissions/{guid}.mp3") so later calls
/// can tell the file kind without extra parameters.
/// Extension whitelisting and size limits belong to the callers, not here.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads under a freshly generated GUID name; <paramref name="fileName"/> is only used for its extension.
    /// Private files cannot be opened by URL; read them through <see cref="GetSignedUrl"/>.
    /// Returns EXTERNAL_STORAGE_FAILED instead of throwing when the provider fails.
    /// </summary>
    Task<Result<FileUploadResponse>> UploadAsync(
        Stream content, string fileName, string folder, bool isPrivate, CancellationToken cancellationToken);

    /// <summary>Builds a short-lived URL for a private file. Computed locally, no network call. Call it only after the ownership check passes.</summary>
    string GetSignedUrl(string publicId);

    /// <summary>
    /// Small preview of the first page of a private image or PDF (at most 160x220, low quality), too small to read
    /// or print. Computed locally, no network call. Null when the file is not an image or PDF.
    /// Product decision (2026-11-10): unlike <see cref="GetSignedUrl"/> this URL never expires, so anyone given the link
    /// can keep viewing the preview; the original file stays behind short-lived URLs. Switch to a private stored
    /// thumbnail if real copyrighted scores must be hidden even as previews.
    /// </summary>
    string? GetThumbnailUrl(string publicId);

    /// <summary>Returns EXTERNAL_STORAGE_FAILED instead of throwing when the provider fails.</summary>
    Task<Result> DeleteAsync(string publicId, bool isPrivate, CancellationToken cancellationToken);
}

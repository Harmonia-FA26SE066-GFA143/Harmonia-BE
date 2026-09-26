using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Stores uploaded files outside the database. The returned <see cref="FileUploadResponse.PublicId"/>
/// includes the file extension (e.g. "harmonia/practice-submissions/{guid}.mp3") so later calls
/// can tell the file kind without extra parameters.
/// Extension whitelisting and size limits belong to the request validators, not here.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads under a freshly generated GUID name; <paramref name="fileName"/> is only used for its extension.
    /// Private files cannot be opened by URL; read them through <see cref="GetSignedUrl"/>.
    /// </summary>
    Task<FileUploadResponse> UploadAsync(Stream content, string fileName, string folder, bool isPrivate, CancellationToken cancellationToken);

    /// <summary>Builds a short-lived URL for a private file. Call it only after the ownership check passes.</summary>
    string GetSignedUrl(string publicId);

    Task DeleteAsync(string publicId, bool isPrivate, CancellationToken cancellationToken);
}

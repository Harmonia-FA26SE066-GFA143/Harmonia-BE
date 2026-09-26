namespace Harmonia.Application.DTOs;

/// <summary>
/// Outcome of a file upload. <see cref="Url"/> is only usable as-is for public files;
/// private files must be served through a signed URL built from <see cref="PublicId"/>.
/// </summary>
public record FileUploadResponse(string PublicId, string Url);

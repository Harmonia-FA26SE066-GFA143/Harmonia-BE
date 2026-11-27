using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>AI that reads the lyrics printed on sheet music. Fails with EXTERNAL_AI_FAILED.</summary>
public interface ILyricsExtractor
{
    /// <summary>
    /// <paramref name="fileUrl"/> is a signed URL of a .pdf, .png or .jpg; <paramref name="fileName"/> gives its extension.
    /// Returns an empty string when the sheet has no lyrics.
    /// </summary>
    Task<Result<string>> ExtractAsync(string fileUrl, string fileName, CancellationToken cancellationToken);
}

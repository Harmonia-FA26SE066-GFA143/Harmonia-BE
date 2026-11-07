using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// A MusicXML/MXL score and the WAV rendered from that same file (Web tbd-backlog B47). Both belong to
/// <see cref="Revision"/>: a replacement stores new files under new ids, so URLs of an older revision never serve
/// the new content. The client can check the downloaded files against the two hashes.
/// </summary>
public class InteractiveScoreDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Always "Ready": conversion and rendering finish inside the upload request, like <see cref="PlaybackDto"/>.</summary>
    public string Status { get; set; } = "Ready";

    /// <summary>1 on upload, +1 on every file replacement.</summary>
    public int Revision { get; set; }

    /// <summary>"musicxml" or "mxl" (zipped).</summary>
    public string ScoreFormat { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    /// <summary>Short-lived signed URL of the score file; request this endpoint again once it expires.</summary>
    public string ScoreUrl { get; set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the score file.</summary>
    public string ScoreSha256 { get; set; } = string.Empty;

    /// <summary>Short-lived signed URL of the WAV rendered from this revision of the score.</summary>
    public string AudioUrl { get; set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the WAV file.</summary>
    public string AudioSha256 { get; set; } = string.Empty;

    /// <summary>Length of the WAV, including a short tail that lets the last notes ring out.</summary>
    public double DurationSeconds { get; set; }

    public PlaybackInstrument Instrument { get; set; }

    /// <summary>When the current revision was saved.</summary>
    public DateTime UpdatedAt { get; set; }
}

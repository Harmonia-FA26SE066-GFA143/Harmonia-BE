using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// A background AI lyrics extraction. Poll until <see cref="Status"/> is Succeeded (read <see cref="LyricsText"/>)
/// or Failed (read <see cref="ErrorCode"/>). Nothing is saved as a material; the director edits the draft and saves it.
/// </summary>
public class LyricsExtractionJobDto
{
    public Guid Id { get; set; }

    public Guid MusicMaterialId { get; set; }

    public LyricsExtractionStatus Status { get; set; }

    public string? LyricsText { get; set; }

    public string? ErrorCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

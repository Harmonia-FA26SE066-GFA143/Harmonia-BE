using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

/// <summary>
/// One AI lyrics extraction from a sheet music material, run in the background so it survives a page reload.
/// The result is a draft kept until <see cref="ExpiresAt"/>; the director saves it as a Lyrics material.
/// </summary>
public class LyricsExtractionJob : BaseAuditableEntity
{
    public Guid MusicMaterialId { get; set; }

    public LyricsExtractionStatus Status { get; set; }

    /// <summary>Set when <see cref="Status"/> is Succeeded.</summary>
    public string? LyricsText { get; set; }

    /// <summary>Set when <see cref="Status"/> is Failed: a code from ErrorCodes.</summary>
    public string? ErrorCode { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Set on completion; after it the job reads as not found and is deleted.</summary>
    public DateTime? ExpiresAt { get; set; }

    public MusicMaterial MusicMaterial { get; set; } = null!;
}

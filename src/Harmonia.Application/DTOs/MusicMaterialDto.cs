using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class MusicMaterialDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public MaterialType MaterialType { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Original name, for display only; the stored file has a generated name. Null for text lyrics.</summary>
    public string? FileName { get; set; }

    public long? FileSizeBytes { get; set; }

    public Guid? TargetSkillId { get; set; }

    public string? TargetSkillName { get; set; }

    /// <summary>Short-lived signed URL; request the list again once it expires. Null for text lyrics.</summary>
    public string? FileUrl { get; set; }

    /// <summary>Lyrics typed as text; null for every material that has a file.</summary>
    public string? LyricsText { get; set; }

    public DateTime CreatedAt { get; set; }
}

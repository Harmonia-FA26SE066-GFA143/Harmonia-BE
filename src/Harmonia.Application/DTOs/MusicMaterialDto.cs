using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class MusicMaterialDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public MaterialType MaterialType { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Original name, for display only; the stored file has a generated name.</summary>
    public string FileName { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public Guid? TargetSkillId { get; set; }

    public string? TargetSkillName { get; set; }

    /// <summary>Short-lived signed URL; request the list again once it expires.</summary>
    public string FileUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

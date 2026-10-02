using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Form fields of the multipart upload; the file itself travels separately as <see cref="FileContent"/>.</summary>
public class UploadMusicMaterialRequest
{
    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; }

    public Guid? TargetSkillId { get; set; }
}

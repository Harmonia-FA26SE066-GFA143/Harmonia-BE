namespace Harmonia.Application.DTOs;

/// <summary>
/// Metadata only. The file and the material type stay as uploaded: a new file or type is a
/// different material, so it is deleted and uploaded again.
/// </summary>
public class UpdateMusicMaterialRequest
{
    public string Title { get; set; } = string.Empty;

    public Guid? TargetSkillId { get; set; }
}

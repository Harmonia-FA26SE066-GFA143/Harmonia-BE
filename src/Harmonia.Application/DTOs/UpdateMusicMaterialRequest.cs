namespace Harmonia.Application.DTOs;

/// <summary>
/// Metadata only. A new file goes through PUT {id}/file (<see cref="ReplaceMusicMaterialFileRequest"/>); a new
/// type is a different material, so it is deleted and uploaded again.
/// </summary>
public class UpdateMusicMaterialRequest
{
    public string Title { get; set; } = string.Empty;

    public Guid? TargetSkillId { get; set; }
}

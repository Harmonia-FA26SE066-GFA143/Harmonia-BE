namespace Harmonia.Application.DTOs;

/// <summary>
/// Metadata only. A new file goes through PUT {id}/file (<see cref="ReplaceMusicMaterialFileRequest"/>); a new
/// type is a different material, so it is deleted and uploaded again.
/// </summary>
public class UpdateMusicMaterialRequest
{
    public string Title { get; set; } = string.Empty;

    public Guid? TargetSkillId { get; set; }

    /// <summary>Required for text lyrics, which have no file to replace; must be empty for every other material.</summary>
    public string? LyricsText { get; set; }
}

using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MusicMaterial : BaseAuditableEntity
{
    public Guid SongId { get; set; }

    public MaterialType MaterialType { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Private file: serve through IFileStorageService.GetSignedUrl, never as a stored URL.
    /// Null only for lyrics typed as text (<see cref="LyricsText"/>).
    /// </summary>
    public string? FilePublicId { get; set; }

    public string? FileName { get; set; }

    /// <summary>Lyrics typed in directly; a Lyrics material has either this or a file, never both.</summary>
    public string? LyricsText { get; set; }

    public long? FileSizeBytes { get; set; }

    public Guid? TargetSkillId { get; set; }

    public bool IsActive { get; set; } = true;

    public Song Song { get; set; } = null!;

    public Skill? TargetSkill { get; set; }

    public ICollection<MaterialLearningProgress> LearningProgresses { get; set; } = [];
}

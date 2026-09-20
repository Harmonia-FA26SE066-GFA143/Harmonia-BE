using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MusicMaterial : BaseAuditableEntity
{
    public Guid SongId { get; set; }

    public MaterialType MaterialType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public Guid? TargetSkillId { get; set; }

    public bool IsActive { get; set; } = true;

    public Song Song { get; set; } = null!;

    public Skill? TargetSkill { get; set; }

    public ICollection<MaterialLearningProgress> LearningProgresses { get; set; } = [];
}

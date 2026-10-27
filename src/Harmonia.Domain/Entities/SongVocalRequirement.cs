using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongVocalRequirement : BaseEntity
{
    public Guid SongId { get; set; }

    public Guid SkillId { get; set; }

    public bool IsMandatory { get; set; }

    /// <summary>How many people the song needs with this skill, e.g. 2 for a duet or 3 solos in the Passion.</summary>
    public int RequiredCount { get; set; } = 1;

    public Song Song { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

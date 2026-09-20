using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongInstrumentRequirement : BaseEntity
{
    public Guid SongId { get; set; }

    public Guid SkillId { get; set; }

    public bool IsMandatory { get; set; }

    public Song Song { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

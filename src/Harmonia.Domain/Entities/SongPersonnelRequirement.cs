using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongPersonnelRequirement : BaseEntity
{
    public Guid SongListItemId { get; set; }

    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }

    public SongListItem SongListItem { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

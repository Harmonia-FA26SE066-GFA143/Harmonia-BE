using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>
/// People a part of the Mass needs whatever song fills it, e.g. a psalmist for the Responsorial Psalm.
/// Copied into SongPersonnelRequirement when a song is put in that slot of a song list; the director may then change it.
/// </summary>
public class LiturgicalSlotRequirement : BaseEntity
{
    public Guid SlotId { get; set; }

    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }

    public LiturgicalSlot Slot { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

/// <summary>
/// People an event needs for the whole service rather than for one song: organist, guitarist, conductor.
/// One member fills it for every song; a song-level requirement of the same skill counts as covered by it.
/// </summary>
public class EventPersonnelRequirement : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

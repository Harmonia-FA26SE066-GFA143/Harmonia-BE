using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class RosterShortage : BaseEntity
{
    public Guid RosterId { get; set; }

    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }

    public int AvailableCount { get; set; }

    public DateTime DetectedAt { get; set; }

    public ServiceRoster Roster { get; set; } = null!;

    public Skill Skill { get; set; } = null!;
}

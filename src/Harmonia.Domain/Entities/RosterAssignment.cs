using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class RosterAssignment : BaseEntity
{
    public Guid RosterId { get; set; }

    public Guid MemberId { get; set; }

    public Guid SkillId { get; set; }

    // Null for a whole-service line (EventPersonnelRequirement), e.g. the organist.
    public Guid? SongListItemId { get; set; }

    public AssignmentSource Source { get; set; }

    public DateTime? NotifiedAt { get; set; }

    public RosterAssignmentStatus Status { get; set; }

    /// <summary>The assignment that took this one's place; set only when Status is Replaced.</summary>
    public Guid? ReplacedByAssignmentId { get; set; }

    /// <summary>Free-text role within the song, e.g. "Narrator" in the Passion or "verse 2" of the psalm.</summary>
    public string? Note { get; set; }

    public ServiceRoster Roster { get; set; } = null!;

    public RosterAssignment? ReplacedByAssignment { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public Skill Skill { get; set; } = null!;

    public SongListItem? SongListItem { get; set; }
}

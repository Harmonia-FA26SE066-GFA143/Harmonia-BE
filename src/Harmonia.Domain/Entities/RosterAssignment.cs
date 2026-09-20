using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class RosterAssignment : BaseEntity
{
    public Guid RosterId { get; set; }

    public Guid MemberId { get; set; }

    public Guid SkillId { get; set; }

    public Guid? SongListItemId { get; set; }

    public AssignmentSource Source { get; set; }

    public DateTime? NotifiedAt { get; set; }

    public ServiceRoster Roster { get; set; } = null!;

    public MemberProfile Member { get; set; } = null!;

    public Skill Skill { get; set; } = null!;

    public SongListItem? SongListItem { get; set; }
}

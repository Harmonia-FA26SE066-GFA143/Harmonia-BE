using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongListItem : BaseEntity
{
    public Guid SongListId { get; set; }

    public Guid SongId { get; set; }

    public Guid SlotId { get; set; }

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }

    public SongList SongList { get; set; } = null!;

    public Song Song { get; set; } = null!;

    public LiturgicalSlot Slot { get; set; } = null!;

    public ICollection<SongPersonnelRequirement> PersonnelRequirements { get; set; } = [];

    public ICollection<RosterAssignment> RosterAssignments { get; set; } = [];
}

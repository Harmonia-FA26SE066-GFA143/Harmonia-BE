using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class Choir : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ChoirMembership> Memberships { get; set; } = [];

    public ICollection<LiturgicalEventChoir> EventChoirs { get; set; } = [];

    public ICollection<SongList> SongLists { get; set; } = [];

    public ICollection<Rehearsal> Rehearsals { get; set; } = [];

    public ICollection<PracticeAssignment> PracticeAssignments { get; set; } = [];
}

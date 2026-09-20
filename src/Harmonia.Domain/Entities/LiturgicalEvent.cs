using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class LiturgicalEvent : BaseAuditableEntity
{
    public Guid WeekId { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public Guid? MassTypeId { get; set; }

    public Guid? CeremonyTypeId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }

    public string? SpecialRequirements { get; set; }

    public EventStatus Status { get; set; }

    public LiturgicalWeek Week { get; set; } = null!;

    public MassType? MassType { get; set; }

    public CeremonyType? CeremonyType { get; set; }

    public EventCategory? Category { get; set; }

    public WorshipLocation Location { get; set; } = null!;

    public ICollection<SongList> SongLists { get; set; } = [];

    public ICollection<EventParticipation> EventParticipations { get; set; } = [];

    public ServiceRoster? ServiceRoster { get; set; }

    public ICollection<Rehearsal> Rehearsals { get; set; } = [];

    public ICollection<PracticeAssignment> PracticeAssignments { get; set; } = [];

    public ICollection<DirectorNote> DirectorNotes { get; set; } = [];
}

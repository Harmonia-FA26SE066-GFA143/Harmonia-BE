using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class Rehearsal : BaseAuditableEntity
{
    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }

    public LiturgicalEvent? LiturgicalEvent { get; set; }

    public WorshipLocation? Location { get; set; }

    public ICollection<RehearsalAttendance> Attendances { get; set; } = [];
}

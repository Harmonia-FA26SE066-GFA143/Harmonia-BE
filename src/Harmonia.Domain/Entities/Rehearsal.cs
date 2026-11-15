using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class Rehearsal : BaseAuditableEntity
{
    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }

    public Guid? ChoirId { get; set; }

    /// <summary>
    /// A rehearsal of every choir serving the event, planned by its lead choir. It has an event and no ChoirId: who
    /// manages, attends and sees it follows the event's choirs, so a change of lead or of serving choirs applies at once.
    /// </summary>
    public bool IsJoint { get; set; }

    public LiturgicalEvent? LiturgicalEvent { get; set; }

    public WorshipLocation? Location { get; set; }

    public Choir? Choir { get; set; }

    public ICollection<RehearsalAttendance> Attendances { get; set; } = [];

    public ICollection<RehearsalSong> Songs { get; set; } = [];

    public ICollection<RehearsalRecording> Recordings { get; set; } = [];
}

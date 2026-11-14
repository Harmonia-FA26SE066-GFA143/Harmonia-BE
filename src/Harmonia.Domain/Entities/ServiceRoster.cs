using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class ServiceRoster : BaseAuditableEntity
{
    public Guid EventId { get; set; }

    public RosterStatus Status { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public Guid? GeneratedBy { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public Guid? FinalizedBy { get; set; }

    /// <summary>
    /// Set when the whole-service lines (organist, conductor) are locked ahead of the song list, so those members can
    /// be told early; song lines stay open until the roster itself is finalized.
    /// </summary>
    public DateTime? WholeServiceFinalizedAt { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public User? Generator { get; set; }

    public User? Finalizer { get; set; }

    public ICollection<RosterAssignment> Assignments { get; set; } = [];
}

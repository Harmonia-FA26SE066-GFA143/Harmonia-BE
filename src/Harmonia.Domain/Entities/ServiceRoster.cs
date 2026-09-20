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

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public User? Generator { get; set; }

    public User? Finalizer { get; set; }

    public ICollection<RosterAssignment> Assignments { get; set; } = [];

    public ICollection<RosterShortage> Shortages { get; set; } = [];
}

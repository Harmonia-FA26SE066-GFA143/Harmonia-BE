using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class ServiceRosterDto
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public RosterStatus Status { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public Guid? FinalizedBy { get; set; }

    /// <summary>Active assignments only; replaced lines are history and are left out.</summary>
    public List<RosterAssignmentDto> Assignments { get; set; } = [];

    /// <summary>Requirements still not fully staffed. A roster may be finalized with shortages.</summary>
    public List<RosterShortageDto> Shortages { get; set; } = [];
}

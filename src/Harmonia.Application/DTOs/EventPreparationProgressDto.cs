using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One member's readiness for an event: rehearsal attendance and practice progress, as raw counts.</summary>
public class EventPreparationProgressDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Null when the member was never asked to confirm.</summary>
    public ParticipationStatus? ParticipationStatus { get; set; }

    /// <summary>Rehearsals of the event that have already started.</summary>
    public int RehearsalsHeld { get; set; }

    /// <summary>Of <see cref="RehearsalsHeld"/>, those recorded Present or Late.</summary>
    public int RehearsalsAttended { get; set; }

    /// <summary>Practice assignments of the event that the member receives.</summary>
    public int AssignmentsTotal { get; set; }

    public int AssignmentsPassed { get; set; }

    public int AssignmentsOverdue { get; set; }
}

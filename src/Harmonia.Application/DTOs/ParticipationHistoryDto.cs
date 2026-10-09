using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One past event in the calling member's participation and practice history (UC-11 / FE-14).</summary>
public class ParticipationHistoryDto
{
    public Guid EventId { get; set; }

    public DateOnly EventDate { get; set; }

    public string? Title { get; set; }

    public string? LiturgicalSeasonName { get; set; }

    /// <summary>Null when the member was never asked to confirm.</summary>
    public ParticipationStatus? ParticipationStatus { get; set; }

    /// <summary>Skill names the member served with on the finalized roster; empty when they did not serve.</summary>
    public List<string> ServedSkills { get; set; } = [];

    public int RehearsalsHeld { get; set; }

    /// <summary>Of <see cref="RehearsalsHeld"/>, those recorded Present or Late.</summary>
    public int RehearsalsAttended { get; set; }

    /// <summary>Practice assignments of the event that the member received.</summary>
    public int AssignmentsTotal { get; set; }

    public int AssignmentsPassed { get; set; }
}

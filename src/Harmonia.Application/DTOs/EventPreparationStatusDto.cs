using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>How ready the whole choir is for an event, as raw counts (UC-15 / FE-21).</summary>
public class EventPreparationStatusDto
{
    public Guid EventId { get; set; }

    public string? Title { get; set; }

    public DateOnly EventDate { get; set; }

    public EventStatus EventStatus { get; set; }

    /// <summary>Status of the newest song list version; null when none was proposed.</summary>
    public SongListStatus? SongListStatus { get; set; }

    public int ParticipationInvited { get; set; }

    public int ParticipationConfirmed { get; set; }

    public int ParticipationDeclined { get; set; }

    public int ParticipationUnsure { get; set; }

    /// <summary>Null when the event has no roster yet.</summary>
    public RosterStatus? RosterStatus { get; set; }

    public int RosterActiveAssignments { get; set; }

    /// <summary>Null when the event has no approved song list, so staffing needs are not known yet.</summary>
    public List<RosterShortageDto>? RosterShortages { get; set; }

    public int RehearsalsTotal { get; set; }

    /// <summary>Rehearsals that have already started.</summary>
    public int RehearsalsHeld { get; set; }

    /// <summary><see cref="RehearsalsHeld"/> times the number of active members.</summary>
    public int AttendanceExpected { get; set; }

    /// <summary>Attendances recorded Present or Late, summed over active members.</summary>
    public int AttendancePresent { get; set; }

    /// <summary>Event assignments each active member receives, summed over members.</summary>
    public int PracticeExpected { get; set; }

    public int PracticePassed { get; set; }

    public int PracticeOverdue { get; set; }
}

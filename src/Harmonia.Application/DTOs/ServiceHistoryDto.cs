namespace Harmonia.Application.DTOs;

/// <summary>One past event the choir served, with how its preparation ended, as raw counts (UC-16 / FE-22).</summary>
public class ServiceHistoryDto
{
    public Guid EventId { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public string? Title { get; set; }

    public string? LiturgicalSeasonName { get; set; }

    public string LocationName { get; set; } = string.Empty;

    /// <summary>Items of the approved song list; 0 when no list was approved.</summary>
    public int SongCount { get; set; }

    /// <summary>Distinct members with an Active line on a Finalized roster; 0 when the roster was never finalized.</summary>
    public int MembersServed { get; set; }

    public int ParticipationConfirmed { get; set; }

    public int ParticipationDeclined { get; set; }

    /// <summary>Rehearsals of the event that have already started.</summary>
    public int RehearsalsHeld { get; set; }

    /// <summary>Attendances recorded Present or Late over <see cref="RehearsalsHeld"/>.</summary>
    public int AttendancePresent { get; set; }
}

namespace Harmonia.Application.DTOs;

/// <summary>An upcoming, not cancelled event whose day got a different celebration or rank from the calendar sync.</summary>
public class SyncAffectedEventDto
{
    public Guid EventId { get; set; }

    public string? Title { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }

    public string? PreviousCelebrationName { get; set; }

    public string? CelebrationName { get; set; }
}

namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/mass-schedules/{id}. A switched-off schedule is skipped when events are generated.</summary>
public class UpdateMassScheduleRequest
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly Time { get; set; }

    public Guid MassTypeId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }

    public bool IsActive { get; set; }
}

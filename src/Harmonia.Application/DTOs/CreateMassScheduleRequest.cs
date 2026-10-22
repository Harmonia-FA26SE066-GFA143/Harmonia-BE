namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/mass-schedules. Day + time + location is unique across schedules.</summary>
public class CreateMassScheduleRequest
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly Time { get; set; }

    public Guid MassTypeId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }
}

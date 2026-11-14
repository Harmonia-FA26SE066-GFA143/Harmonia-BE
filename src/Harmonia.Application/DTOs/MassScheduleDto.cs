namespace Harmonia.Application.DTOs;

public class MassScheduleDto
{
    public Guid Id { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly Time { get; set; }

    public Guid MassTypeId { get; set; }

    public Guid LocationId { get; set; }

    public string? Title { get; set; }

    public bool IsActive { get; set; }
}

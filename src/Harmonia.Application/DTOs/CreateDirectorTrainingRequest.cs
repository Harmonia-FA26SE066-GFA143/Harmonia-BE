namespace Harmonia.Application.DTOs;

public class CreateDirectorTrainingRequest
{
    public string CourseName { get; set; } = string.Empty;

    public string? Organizer { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }
}

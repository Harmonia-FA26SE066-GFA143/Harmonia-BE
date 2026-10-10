namespace Harmonia.Application.DTOs;

public class WorshipLocationDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }
}

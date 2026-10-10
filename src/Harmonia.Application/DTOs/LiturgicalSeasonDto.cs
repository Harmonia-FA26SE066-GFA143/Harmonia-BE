namespace Harmonia.Application.DTOs;

public class LiturgicalSeasonDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string? ColorHex { get; set; }

    public bool IsActive { get; set; }
}

namespace Harmonia.Application.DTOs;

public class LiturgicalDayDto
{
    public DateOnly Date { get; set; }

    public string CelebrationName { get; set; } = string.Empty;

    public string? Rank { get; set; }

    public string? SeasonName { get; set; }
}
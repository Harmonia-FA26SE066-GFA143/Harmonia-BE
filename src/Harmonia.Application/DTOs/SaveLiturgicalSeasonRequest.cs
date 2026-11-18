namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a liturgical season (UC-32 / FE-50).</summary>
public class SaveLiturgicalSeasonRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The calendar season the row stands for; without it both dates are required.</summary>
    public string? CalendarKey { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ColorHex { get; set; }

    public bool IsActive { get; set; } = true;
}

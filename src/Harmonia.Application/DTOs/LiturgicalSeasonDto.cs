namespace Harmonia.Application.DTOs;

public class LiturgicalSeasonDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Advent, Christmas, Lent, Easter or OrdinaryTime; null on a season described by dates.</summary>
    public string? CalendarKey { get; set; }

    /// <summary>Null on a keyed season, which spans every year.</summary>
    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ColorHex { get; set; }

    public bool IsActive { get; set; }
}

using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

// Cached result of the external Catholic calendar API for one date.
// No foreign key: LiturgicalEvent is matched by EventDate = Date.
public class LiturgicalDay : BaseEntity
{
    public DateOnly Date { get; set; }

    public string CelebrationName { get; set; } = string.Empty;

    public string? Rank { get; set; }

    public string? SeasonName { get; set; }

    // white, red, green, purple, rose or black, as the calendar returns it; null until the year is synced again.
    public string? Color { get; set; }

    public DateTime FetchedAt { get; set; }
}

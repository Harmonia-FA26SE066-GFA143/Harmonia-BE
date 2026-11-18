using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class LiturgicalSeason : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The calendar season this row stands for (<see cref="LiturgicalCalendarSeasons"/>), one active row per key; events
    /// then take their season from the synced calendar. Null on rows that still describe one year by dates.
    /// </summary>
    public string? CalendarKey { get; set; }

    /// <summary>Required when <see cref="CalendarKey"/> is null; a keyed season spans every year.</summary>
    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ColorHex { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LiturgicalEvent> LiturgicalEvents { get; set; } = [];
}

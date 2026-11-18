using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

/// <summary>
/// Picks an event's catalog season (B64): the season of its day in the synced calendar, else the dated season holding
/// the date. Pure, so the per-event lookup and the bulk generation from Mass schedules share one rule.
/// </summary>
public static class LiturgicalSeasonResolver
{
    /// <summary>A Mass from this hour on the eve of a new season is its vigil and belongs to that season (24/12 evening → Christmas).</summary>
    public static readonly TimeOnly VigilFrom = new(17, 0);

    /// <summary>
    /// Calendar season key of an event at <paramref name="date"/> <paramref name="time"/>; null when that day is not synced.
    /// <paramref name="dayKeys"/> holds the synced season of the date and of the next day, when known.
    /// </summary>
    public static string? CalendarKeyOf(DateOnly date, TimeOnly time, IReadOnlyDictionary<DateOnly, string?> dayKeys)
    {
        if (!dayKeys.TryGetValue(date, out var key) || key is null) return null;

        return time >= VigilFrom && dayKeys.TryGetValue(date.AddDays(1), out var next) && next is not null && next != key
            ? next
            : key;
    }

    /// <summary>The active season for the event, or null; <paramref name="seasons"/> are the active rows.</summary>
    public static Guid? Resolve(
        DateOnly date, TimeOnly time, IReadOnlyDictionary<DateOnly, string?> dayKeys, IReadOnlyCollection<LiturgicalSeason> seasons)
    {
        var key = CalendarKeyOf(date, time, dayKeys);
        var keyed = key is null ? null : seasons.FirstOrDefault(x => x.CalendarKey == key);

        return keyed?.Id
            ?? seasons.FirstOrDefault(x => x.CalendarKey is null && x.StartDate <= date && date <= x.EndDate)?.Id;
    }

    /// <summary>Sort position of a keyed season in the liturgical year; dated seasons come after.</summary>
    public static int OrderOf(LiturgicalSeason season)
    {
        var index = season.CalendarKey is { } key ? LiturgicalCalendarSeasons.All.ToList().IndexOf(key) : -1;
        return index < 0 ? LiturgicalCalendarSeasons.All.Count : index;
    }
}

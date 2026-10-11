namespace Harmonia.Domain.Common;

/// <summary>
/// Calendar date at the parish. Timestamps are stored in UTC; only "is it today / in the past" checks
/// against <see cref="DateOnly"/> fields use this, so they do not depend on the server time zone.
/// </summary>
public static class VietnamTime
{
    // Vietnam is UTC+7 all year (no daylight saving), so a fixed offset avoids relying on the
    // host's time zone database (IANA ids need ICU on Windows hosts).
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow + Offset);

    /// <summary>A stored UTC timestamp as the wall-clock time at the parish, for text a person reads.</summary>
    public static DateTime FromUtc(DateTime utc) => utc + Offset;

    /// <summary>The UTC instant at which <paramref name="date"/> begins at the parish, for filtering UTC timestamps by date.</summary>
    public static DateTime StartOfDayUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - Offset;
}

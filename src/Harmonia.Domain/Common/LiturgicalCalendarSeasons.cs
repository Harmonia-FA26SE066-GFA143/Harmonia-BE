namespace Harmonia.Domain.Common;

/// <summary>
/// Season keys of the synced calendar (LiturgicalDay.SeasonName), also the CalendarKey a catalog LiturgicalSeason
/// stands for. Listed in liturgical-year order.
/// </summary>
public static class LiturgicalCalendarSeasons
{
    public const string Advent = "Advent";

    public const string Christmas = "Christmas";

    public const string Lent = "Lent";

    public const string Easter = "Easter";

    public const string OrdinaryTime = "OrdinaryTime";

    public static readonly IReadOnlyList<string> All = [Advent, Christmas, Lent, Easter, OrdinaryTime];
}

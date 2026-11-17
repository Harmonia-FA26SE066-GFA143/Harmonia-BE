using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Tests;

public class LiturgicalSeasonResolverTests
{
    private static readonly DateOnly Eve = new(2026, 12, 24);

    private readonly LiturgicalSeason _advent = new() { Id = Guid.NewGuid(), CalendarKey = LiturgicalCalendarSeasons.Advent };
    private readonly LiturgicalSeason _christmas = new() { Id = Guid.NewGuid(), CalendarKey = LiturgicalCalendarSeasons.Christmas };
    private readonly LiturgicalSeason _dated = new()
    {
        Id = Guid.NewGuid(), StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 1, 31),
    };

    private static Dictionary<DateOnly, string?> Synced() => new()
    {
        [Eve] = LiturgicalCalendarSeasons.Advent,
        [Eve.AddDays(1)] = LiturgicalCalendarSeasons.Christmas,
    };

    [Theory]
    [InlineData(9, LiturgicalCalendarSeasons.Advent)]
    [InlineData(16, LiturgicalCalendarSeasons.Advent)]
    [InlineData(17, LiturgicalCalendarSeasons.Christmas)]  // evening vigil takes the next day's season
    [InlineData(22, LiturgicalCalendarSeasons.Christmas)]
    public void CalendarKeyOf_EveOfANewSeason_SwitchesFromFivePm(int hour, string expected)
    {
        Assert.Equal(expected, LiturgicalSeasonResolver.CalendarKeyOf(Eve, new TimeOnly(hour, 0), Synced()));
    }

    [Fact]
    public void CalendarKeyOf_EveningWithinOneSeason_KeepsTheDaysSeason()
    {
        var days = new Dictionary<DateOnly, string?>
        {
            [Eve] = LiturgicalCalendarSeasons.Advent, [Eve.AddDays(1)] = LiturgicalCalendarSeasons.Advent,
        };

        Assert.Equal(LiturgicalCalendarSeasons.Advent, LiturgicalSeasonResolver.CalendarKeyOf(Eve, new TimeOnly(19, 0), days));
    }

    [Fact]
    public void Resolve_SyncedDay_PicksTheKeyedSeason()
    {
        var id = LiturgicalSeasonResolver.Resolve(Eve, new TimeOnly(19, 0), Synced(), [_advent, _christmas, _dated]);

        Assert.Equal(_christmas.Id, id);
    }

    [Fact]
    public void Resolve_DayNotSynced_FallsBackToTheDatedSeason()
    {
        var date = new DateOnly(2027, 1, 10);

        Assert.Equal(_dated.Id, LiturgicalSeasonResolver.Resolve(date, new TimeOnly(9, 0), Synced(), [_advent, _dated]));
        Assert.Null(LiturgicalSeasonResolver.Resolve(date.AddMonths(2), new TimeOnly(9, 0), Synced(), [_advent, _dated]));
    }

    [Fact]
    public void Resolve_SyncedDayWithoutAKeyedRow_FallsBackToTheDatedSeason()
    {
        var date = new DateOnly(2027, 1, 10);
        var days = new Dictionary<DateOnly, string?> { [date] = LiturgicalCalendarSeasons.OrdinaryTime };

        Assert.Equal(_dated.Id, LiturgicalSeasonResolver.Resolve(date, new TimeOnly(9, 0), days, [_advent, _dated]));
    }
}

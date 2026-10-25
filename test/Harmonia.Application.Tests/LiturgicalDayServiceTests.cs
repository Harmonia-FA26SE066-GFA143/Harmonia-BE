using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class LiturgicalDayServiceTests
{
    private readonly ILiturgicalDayRepository _days = Substitute.For<ILiturgicalDayRepository>();
    private readonly ILiturgicalCalendarClient _calendar = Substitute.For<ILiturgicalCalendarClient>();
    private readonly ILiturgicalNameTranslationRepository _translations = Substitute.For<ILiturgicalNameTranslationRepository>();
    private readonly LiturgicalDayService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<LiturgicalDay> _cached = [];
    private readonly List<LiturgicalDay> _added = [];

    public LiturgicalDayServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LiturgicalDayProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LiturgicalDayService(_days, _calendar, _translations, mapper);

        _translations.GetAllAsync(_ct).Returns(new Dictionary<(LiturgicalNameKind, string), string>
        {
            [(LiturgicalNameKind.Celebration, "Christmas")] = "Chúa Giáng Sinh",
            [(LiturgicalNameKind.Rank, "Solemnity")] = "Lễ trọng",
            [(LiturgicalNameKind.Season, "Christmas")] = "Mùa Giáng Sinh",
        });
        _days.GetYearForUpdateAsync(Arg.Any<int>(), _ct).Returns(_ => _cached.ToList());
        _days.AddRangeAsync(Arg.Do<IEnumerable<LiturgicalDay>>(_added.AddRange), _ct).Returns(Task.CompletedTask);
    }

    private void CalendarReturns(params CalendarDayResult[] days) =>
        _calendar.GetYearAsync(2026, _ct).Returns(Result<List<CalendarDayResult>>.Success([.. days]));

    [Fact]
    public async Task GetByDate_Cached_ReturnsDay_Async()
    {
        var date = new DateOnly(2026, 12, 25);
        _days.GetByDateAsync(date, _ct).Returns(new LiturgicalDay
        {
            Id = Guid.NewGuid(), Date = date, CelebrationName = "Christmas", Rank = "Solemnity", SeasonName = "Christmas",
        });

        var result = await _sut.GetByDateAsync(date, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            (date, "Christmas", "Solemnity", "Christmas"),
            (result.Value!.Date, result.Value.CelebrationName, result.Value.Rank, result.Value.SeasonName));
        // The same English word reads differently as a celebration and as a season.
        Assert.Equal(
            ("Chúa Giáng Sinh", "Lễ trọng", "Mùa Giáng Sinh"),
            (result.Value.CelebrationNameVi, result.Value.RankVi, result.Value.SeasonNameVi));
    }

    [Theory]
    [InlineData("29th Sunday of Ordinary Time", "Chúa nhật XXIX Thường niên")]
    [InlineData("Tuesday of the 2nd Week of Advent", "Thứ Ba tuần II Mùa Vọng")]
    [InlineData("Third Sunday of Lent", "Chúa nhật III Mùa Chay")]
    [InlineData("Saint New To The Calendar", "Saint New To The Calendar")]
    public async Task GetRange_NameWithoutRow_UsesThePatternOrStaysEnglish_Async(string english, string vietnamese)
    {
        var date = new DateOnly(2026, 10, 18);
        _days.GetRangeAsync(date, date, _ct).Returns([new LiturgicalDay { Date = date, CelebrationName = english }]);

        var day = Assert.Single((await _sut.GetRangeAsync(new SearchLiturgicalDaysRequest { FromDate = date, ToDate = date }, _ct)).Value!);

        Assert.Equal(vietnamese, day.CelebrationNameVi);
        Assert.Null(day.RankVi);
    }

    [Fact]
    public async Task GetByDate_NotCached_ReturnsCalendarDayNotFound_Async()
    {
        var result = await _sut.GetByDateAsync(new DateOnly(2026, 12, 25), _ct);

        Assert.Equal(ErrorCodes.CalendarDayNotFound, result.Code);
    }

    [Fact]
    public async Task Sync_CalendarFails_ReturnsItsCode_AndSavesNothing_Async()
    {
        _calendar.GetYearAsync(2026, _ct)
            .Returns(Result<List<CalendarDayResult>>.Failure(ErrorCodes.ExternalCalendarFailed));

        var result = await _sut.SyncAsync(new SyncLiturgicalDaysRequest { Year = 2026 }, _ct);

        Assert.Equal(ErrorCodes.ExternalCalendarFailed, result.Code);
        await _days.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Sync_AddsNewDays_AndOverwritesCachedOnes_Async()
    {
        var cachedDate = new DateOnly(2026, 12, 25);
        var newDate = new DateOnly(2026, 12, 26);
        var cachedRow = new LiturgicalDay
        {
            Id = Guid.NewGuid(), Date = cachedDate, CelebrationName = "Old cached name", Rank = null, SeasonName = null,
            FetchedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _cached.Add(cachedRow);
        CalendarReturns(
            new CalendarDayResult(cachedDate, "Christmas", "Solemnity", "Christmas"),
            new CalendarDayResult(newDate, "Saint Stephen, The First Martyr", "Feast", "Christmas"));

        var result = await _sut.SyncAsync(new SyncLiturgicalDaysRequest { Year = 2026 }, _ct);

        Assert.Equal(2, result.Value);
        Assert.Equal(
            ("Christmas", "Solemnity", "Christmas"),
            (cachedRow.CelebrationName, cachedRow.Rank, cachedRow.SeasonName));
        Assert.True(cachedRow.FetchedAt > new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var added = Assert.Single(_added);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal(
            (newDate, "Saint Stephen, The First Martyr", "Feast", "Christmas"),
            (added.Date, added.CelebrationName, added.Rank, added.SeasonName));
        await _days.Received(1).SaveChangesAsync(_ct);
    }
}

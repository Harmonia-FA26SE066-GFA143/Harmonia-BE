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
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ILiturgicalDayRepository _days = Substitute.For<ILiturgicalDayRepository>();
    private readonly ILiturgicalCalendarClient _calendar = Substitute.For<ILiturgicalCalendarClient>();
    private readonly ILiturgicalNameTranslationRepository _translations = Substitute.For<ILiturgicalNameTranslationRepository>();
    private readonly LiturgicalDayService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<LiturgicalDay> _cached = [];
    private readonly List<LiturgicalDay> _added = [];
    private readonly List<LiturgicalEvent> _eventRows = [];

    public LiturgicalDayServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LiturgicalDayProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LiturgicalDayService(_days, _calendar, _translations, _events, _users, _notifications, mapper);
        _events.ListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<LiturgicalEvent, bool>>>(), _ct)
            .Returns(call => _eventRows
                .Where(call.Arg<System.Linq.Expressions.Expression<Func<LiturgicalEvent, bool>>>().Compile()).ToList());

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
            new CalendarDayResult(cachedDate, "Christmas", "Solemnity", "Christmas", "white"),
            new CalendarDayResult(newDate, "Saint Stephen, The First Martyr", "Feast", "Christmas"));

        var result = await _sut.SyncAsync(new SyncLiturgicalDaysRequest { Year = 2026 }, _ct);

        Assert.Equal(2, result.Value!.SyncedCount);
        Assert.Equal(
            ("Christmas", "Solemnity", "Christmas", "white"),
            (cachedRow.CelebrationName, cachedRow.Rank, cachedRow.SeasonName, cachedRow.Color));
        Assert.True(cachedRow.FetchedAt > new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var added = Assert.Single(_added);
        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal(
            (newDate, "Saint Stephen, The First Martyr", "Feast", "Christmas"),
            (added.Date, added.CelebrationName, added.Rank, added.SeasonName));
        await _days.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Sync_ChangedCelebrationOnPlannedDay_ReturnsAffectedEvents_AndTellsPriests_Async()
    {
        var changed = VietnamTime.Today.AddDays(10);
        var unchanged = changed.AddDays(1);
        _cached.AddRange(
        [
            new LiturgicalDay { Id = Guid.NewGuid(), Date = changed, CelebrationName = "Old feast" },
            new LiturgicalDay { Id = Guid.NewGuid(), Date = unchanged, CelebrationName = "Same" },
        ]);
        var planned = new LiturgicalEvent { Id = Guid.NewGuid(), EventDate = changed, Status = EventStatus.Draft, Title = "Mass" };
        _eventRows.AddRange(
        [
            planned,
            new LiturgicalEvent { Id = Guid.NewGuid(), EventDate = changed, Status = EventStatus.Cancelled },
            new LiturgicalEvent { Id = Guid.NewGuid(), EventDate = unchanged, Status = EventStatus.Published },
        ]);
        _days.GetYearForUpdateAsync(changed.Year, _ct).Returns(_ => _cached.ToList());
        _calendar.GetYearAsync(changed.Year, _ct).Returns(Result<List<CalendarDayResult>>.Success(
        [
            new CalendarDayResult(changed, "New feast", "Feast", "OrdinaryTime"),
            new CalendarDayResult(unchanged, "Same", null, "OrdinaryTime"),
        ]));
        var priestId = Guid.NewGuid();
        _users.GetActiveUserIdsByRolesAsync(Arg.Any<IReadOnlyCollection<string>>(), _ct).Returns([priestId]);

        var result = await _sut.SyncAsync(new SyncLiturgicalDaysRequest { Year = changed.Year }, _ct);

        var affected = Assert.Single(result.Value!.AffectedEvents);
        Assert.Equal((planned.Id, "Old feast", "New feast"), (affected.EventId, affected.PreviousCelebrationName, affected.CelebrationName));
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.LiturgicalCalendarChanged
                && r.RecipientUserIds.SequenceEqual(new[] { priestId })),
            _ct);
    }
}

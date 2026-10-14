using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class LiturgicalDayServiceTests
{
    private const long MaxFileSizeBytes = 2 * 1024 * 1024;

    private readonly ILiturgicalDayRepository _days = Substitute.For<ILiturgicalDayRepository>();
    private readonly ICatholicCalendarParser _parser = Substitute.For<ICatholicCalendarParser>();
    private readonly LiturgicalDayService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Stream _file = new MemoryStream([1]);
    private readonly List<LiturgicalDay> _added = [];

    public LiturgicalDayServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LiturgicalDayProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LiturgicalDayService(_days, _parser, mapper);

        _days.GetCachedDatesInYearAsync(Arg.Any<int>(), _ct).Returns(new HashSet<DateOnly>());
        _days.AddRangeAsync(Arg.Do<IEnumerable<LiturgicalDay>>(_added.AddRange), _ct).Returns(Task.CompletedTask);
    }

    private void ParserReturns(params CalendarDayResult[] days) =>
        _parser.ParseAsync(_file, _ct).Returns(Result<List<CalendarDayResult>>.Success([.. days]));

    [Fact]
    public async Task GetByDate_Cached_ReturnsDay_Async()
    {
        var date = new DateOnly(2026, 12, 25);
        _days.GetByDateAsync(date, _ct).Returns(new LiturgicalDay
        {
            Id = Guid.NewGuid(), Date = date, CelebrationName = "Christmas", Rank = "Solemnity", SeasonName = "Christmas season",
        });

        var result = await _sut.GetByDateAsync(date, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            (date, "Christmas", "Solemnity", "Christmas season"),
            (result.Value!.Date, result.Value.CelebrationName, result.Value.Rank, result.Value.SeasonName));
    }

    [Fact]
    public async Task GetByDate_NotCached_ReturnsCalendarDayNotFound_Async()
    {
        var result = await _sut.GetByDateAsync(new DateOnly(2026, 12, 25), _ct);

        Assert.Equal(ErrorCodes.CalendarDayNotFound, result.Code);
    }

    [Theory]
    [InlineData("calendar.ics", 0, ErrorCodes.CalendarFileRequired)]
    [InlineData("calendar.txt", 10, ErrorCodes.CalendarFileTypeNotAllowed)]
    [InlineData("calendar", 10, ErrorCodes.CalendarFileTypeNotAllowed)]
    [InlineData("calendar.ics", MaxFileSizeBytes + 1, ErrorCodes.CalendarFileTooLarge)]
    public async Task Import_BadFile_ReturnsCode_AndNeverParses_Async(string fileName, long length, string expectedCode)
    {
        var result = await _sut.ImportAsync(_file, fileName, length, _ct);

        Assert.Equal(expectedCode, result.Code);
        await _parser.DidNotReceiveWithAnyArgs().ParseAsync(default!, _ct);
    }

    [Fact]
    public async Task Import_ParserFails_ReturnsParserCode_AndSavesNothing_Async()
    {
        _parser.ParseAsync(_file, _ct).Returns(Result<List<CalendarDayResult>>.Failure("PARSER_CODE"));

        var result = await _sut.ImportAsync(_file, "calendar.ics", 10, _ct);

        Assert.Equal("PARSER_CODE", result.Code);
        await _days.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Import_UpperCaseExtensionAtMaxSize_StoresEveryParsedDay_Async()
    {
        var date = new DateOnly(2026, 12, 25);
        ParserReturns(new CalendarDayResult(date, "Christmas", "Solemnity", "Christmas season"));

        var result = await _sut.ImportAsync(_file, "CALENDAR.ICS", MaxFileSizeBytes, _ct);

        Assert.Equal(1, result.Value);
        var day = Assert.Single(_added);
        Assert.NotEqual(Guid.Empty, day.Id);
        Assert.Equal(
            (date, "Christmas", "Solemnity", "Christmas season"),
            (day.Date, day.CelebrationName, day.Rank, day.SeasonName));
        Assert.NotEqual(default, day.FetchedAt);
        await _days.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Import_FeedSpansTwoYears_SkipsDatesAlreadyCachedInEachYear_Async()
    {
        var cachedIn2026 = new DateOnly(2026, 12, 31);
        var newIn2026 = new DateOnly(2026, 12, 30);
        var cachedIn2027 = new DateOnly(2027, 1, 1);
        var newIn2027 = new DateOnly(2027, 1, 2);
        _days.GetCachedDatesInYearAsync(2026, _ct).Returns(new HashSet<DateOnly> { cachedIn2026 });
        _days.GetCachedDatesInYearAsync(2027, _ct).Returns(new HashSet<DateOnly> { cachedIn2027 });
        ParserReturns(
            new CalendarDayResult(cachedIn2026, "A", null, null),
            new CalendarDayResult(newIn2026, "B", null, null),
            new CalendarDayResult(cachedIn2027, "C", null, null),
            new CalendarDayResult(newIn2027, "D", null, null));

        var result = await _sut.ImportAsync(_file, "calendar.ics", 10, _ct);

        Assert.Equal(2, result.Value);
        Assert.Equal([newIn2026, newIn2027], _added.Select(d => d.Date));
    }

    [Fact]
    public async Task Import_EverythingAlreadyCached_ReturnsZero_AndSavesNothing_Async()
    {
        var date = new DateOnly(2026, 12, 25);
        _days.GetCachedDatesInYearAsync(2026, _ct).Returns(new HashSet<DateOnly> { date });
        ParserReturns(new CalendarDayResult(date, "Christmas", null, null));

        var result = await _sut.ImportAsync(_file, "calendar.ics", 10, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        await _days.DidNotReceiveWithAnyArgs().AddRangeAsync(default!, _ct);
        await _days.DidNotReceive().SaveChangesAsync(_ct);
    }
}

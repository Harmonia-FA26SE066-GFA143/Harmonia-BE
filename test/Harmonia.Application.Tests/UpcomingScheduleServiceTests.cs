using AutoMapper;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class UpcomingScheduleServiceTests
{
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly UpcomingScheduleService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly WorshipLocation _church = new() { Id = Guid.NewGuid(), Name = "Main church" };

    public UpcomingScheduleServiceTests()
    {
        var mapper = new MapperConfiguration(
                cfg =>
                {
                    cfg.AddProfile<LiturgicalEventProfile>();
                    cfg.AddProfile<RehearsalProfile>();
                    cfg.AddProfile<RehearsalSongProfile>();
                },
                NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new UpcomingScheduleService(_events, _rehearsals, mapper);
    }

    [Fact]
    public async Task GetUpcomingEvents_AsksFromTodayInVietnam_AndKeepsRepositoryOrder_Async()
    {
        var first = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = new DateOnly(2026, 12, 24), Time = new TimeOnly(19, 0), Title = "Vigil",
            Location = _church, Status = EventStatus.Published,
        };
        var second = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = new DateOnly(2026, 12, 25), Time = new TimeOnly(8, 0),
            Location = _church, Status = EventStatus.Published,
        };
        var fromDates = new List<DateOnly>();
        _events.GetUpcomingPublishedAsync(Arg.Do<DateOnly>(fromDates.Add), _ct).Returns([first, second]);
        var before = VietnamTime.Today;

        var result = await _sut.GetUpcomingEventsAsync(_ct);

        // A range, so the test cannot fail when it runs across midnight.
        Assert.InRange(Assert.Single(fromDates), before, VietnamTime.Today);
        Assert.True(result.IsSuccess);
        Assert.Equal([first.Id, second.Id], result.Value!.Select(e => e.Id));
        var dto = result.Value![0];
        Assert.Equal(
            (first.EventDate, first.Time, "Vigil", "Main church", EventStatus.Published),
            (dto.EventDate, dto.Time, dto.Title, dto.LocationName, dto.Status));
    }

    [Fact]
    public async Task GetUpcomingEvents_None_ReturnsEmpty_Async()
    {
        _events.GetUpcomingPublishedAsync(Arg.Any<DateOnly>(), _ct).Returns([]);

        var result = await _sut.GetUpcomingEventsAsync(_ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetUpcomingRehearsals_AsksFromNowUtc_AndMapsLocationAndSongsInOrder_Async()
    {
        var kyrie = new Song { Id = Guid.NewGuid(), Title = "Kyrie" };
        var gloria = new Song { Id = Guid.NewGuid(), Title = "Gloria" };
        var start = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        var withLocation = new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(2), Location = _church, Note = "Bring folders",
            Songs =
            [
                new() { SongId = kyrie.Id, Song = kyrie, DisplayOrder = 2 },
                new() { SongId = gloria.Id, Song = gloria, DisplayOrder = 1 },
            ],
        };
        var withoutLocation = new Rehearsal { Id = Guid.NewGuid(), StartTime = start.AddDays(1), EndTime = start.AddDays(1).AddHours(1) };
        var fromTimes = new List<DateTime>();
        _rehearsals.GetUpcomingAsync(Arg.Do<DateTime>(fromTimes.Add), _ct).Returns([withLocation, withoutLocation]);
        var before = DateTime.UtcNow;

        var result = await _sut.GetUpcomingRehearsalsAsync(_ct);

        Assert.InRange(Assert.Single(fromTimes), before, DateTime.UtcNow);
        Assert.True(result.IsSuccess);
        Assert.Equal([withLocation.Id, withoutLocation.Id], result.Value!.Select(r => r.Id));
        var dto = result.Value![0];
        Assert.Equal((start, start.AddHours(2), "Main church", "Bring folders"), (dto.StartTime, dto.EndTime, dto.LocationName, dto.Note));
        Assert.Equal([gloria.Id, kyrie.Id], dto.Songs.Select(s => s.SongId));
        Assert.Null(result.Value[1].LocationName);
        Assert.Empty(result.Value[1].Songs);
    }
}

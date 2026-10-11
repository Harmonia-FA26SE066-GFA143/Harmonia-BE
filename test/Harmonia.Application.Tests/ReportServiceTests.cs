using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Services;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class ReportServiceTests
{
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly ReportService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly SearchReportsRequest _request = new();

    public ReportServiceTests() => _sut = new ReportService(_events, _songs);

    private void ReturnsEvents(params LiturgicalEvent[] events) =>
        _events.GetServiceHistoryAsync(_request, Arg.Any<DateOnly>(), _ct)
            .Returns(new PagedList<LiturgicalEvent>([.. events], 1, 20, events.Length));

    private static LiturgicalEvent NewEvent() =>
        new() { Id = Guid.NewGuid(), Location = new WorshipLocation { Name = "Main church" } };

    [Fact]
    public async Task GetServiceHistory_CountsApprovedSongsActiveFinalizedMembersAndHeldRehearsals_Async()
    {
        var (alice, bob) = (Guid.NewGuid(), Guid.NewGuid());
        var past = DateTime.UtcNow.AddDays(-1);
        var liturgicalEvent = NewEvent();
        liturgicalEvent.LiturgicalSeason = new LiturgicalSeason { Name = "Advent" };
        liturgicalEvent.SongLists =
        [
            new SongList { Version = 1, Status = SongListStatus.Rejected, Items = [new SongListItem()] },
            new SongList { Version = 2, Status = SongListStatus.Approved, Items = [new SongListItem(), new SongListItem()] },
        ];
        liturgicalEvent.ServiceRoster = new ServiceRoster
        {
            Status = RosterStatus.Finalized,
            Assignments =
            [
                new RosterAssignment { MemberId = alice, Status = RosterAssignmentStatus.Active },
                new RosterAssignment { MemberId = alice, Status = RosterAssignmentStatus.Active },
                new RosterAssignment { MemberId = bob, Status = RosterAssignmentStatus.Replaced },
            ],
        };
        liturgicalEvent.EventParticipations =
        [
            new EventParticipation { Status = ParticipationStatus.Confirmed },
            new EventParticipation { Status = ParticipationStatus.Declined },
            new EventParticipation { Status = ParticipationStatus.Invited },
        ];
        liturgicalEvent.Rehearsals =
        [
            new Rehearsal
            {
                StartTime = past,
                Attendances =
                [
                    new RehearsalAttendance { Status = AttendanceStatus.Present },
                    new RehearsalAttendance { Status = AttendanceStatus.Late },
                    new RehearsalAttendance { Status = AttendanceStatus.Absent },
                ],
            },
            new Rehearsal { StartTime = DateTime.UtcNow.AddDays(1), Attendances = [new RehearsalAttendance { Status = AttendanceStatus.Present }] },
        ];
        ReturnsEvents(liturgicalEvent);

        var result = await _sut.GetServiceHistoryAsync(_request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.TotalCount);
        var row = Assert.Single(result.Value.Items);
        Assert.Equal(("Advent", "Main church"), (row.LiturgicalSeasonName, row.LocationName));
        Assert.Equal((2, 1), (row.SongCount, row.MembersServed));
        Assert.Equal((1, 1), (row.ParticipationConfirmed, row.ParticipationDeclined));
        Assert.Equal((1, 2), (row.RehearsalsHeld, row.AttendancePresent));
    }

    [Fact]
    public async Task GetServiceHistory_NoApprovedListAndRosterNotFinalized_ReportsZero_Async()
    {
        var liturgicalEvent = NewEvent();
        liturgicalEvent.ServiceRoster = new ServiceRoster
        {
            Status = RosterStatus.Suggested,
            Assignments = [new RosterAssignment { MemberId = Guid.NewGuid(), Status = RosterAssignmentStatus.Active }],
        };
        ReturnsEvents(liturgicalEvent);

        var result = await _sut.GetServiceHistoryAsync(_request, _ct);

        var row = Assert.Single(result.Value!.Items);
        Assert.Equal((0, 0), (row.SongCount, row.MembersServed));
        Assert.Null(row.LiturgicalSeasonName);
    }

    [Fact]
    public async Task GetSongUsage_MapsTheRepositoryPage_Async()
    {
        var song = new Song { Id = Guid.NewGuid(), Title = "Kyrie", Composer = "Anon" };
        var lastUsed = new DateOnly(2026, 10, 4);
        _songs.GetUsageAsync(_request, Arg.Any<DateOnly>(), _ct)
            .Returns(new PagedList<(Song, int, DateOnly?)>([(song, 3, lastUsed)], 2, 20, 21));

        var page = (await _sut.GetSongUsageAsync(_request, _ct)).Value!;

        var row = Assert.Single(page.Items);
        Assert.Equal((song.Id, "Kyrie", "Anon", 3, lastUsed), (row.SongId, row.Title, row.Composer, row.TimesUsed, row.LastUsedDate));
        Assert.Equal((2, 20, 21), (page.PageNumber, page.PageSize, page.TotalCount));
    }
}

using System.Linq.Expressions;
using AutoMapper;
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

public class RehearsalServiceTests
{
    private static readonly DateTime Start = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IGenericRepository<WorshipLocation> _locations = Substitute.For<IGenericRepository<WorshipLocation>>();
    private readonly IGenericRepository<Song> _songs = Substitute.For<IGenericRepository<Song>>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly RehearsalService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<Rehearsal> _added = [];
    private readonly LiturgicalEvent _event = new() { Id = Guid.NewGuid(), Status = EventStatus.Published };
    private readonly WorshipLocation _hall = new() { Id = Guid.NewGuid(), Name = "Parish hall", IsActive = true };
    private readonly Song _kyrie = new() { Id = Guid.NewGuid(), Title = "Kyrie" };
    private readonly Song _gloria = new() { Id = Guid.NewGuid(), Title = "Gloria" };
    private readonly Song _retired = new() { Id = Guid.NewGuid(), Title = "Retired", IsActive = false };

    public RehearsalServiceTests()
    {
        var mapper = new MapperConfiguration(
                cfg =>
                {
                    cfg.AddProfile<RehearsalProfile>();
                    cfg.AddProfile<RehearsalSongProfile>();
                },
                NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new RehearsalService(_rehearsals, _events, _locations, _songs, _users, _notifications, mapper);

        _events.GetByIdAsync(_event.Id, _ct).Returns(_event);
        _locations.GetByIdAsync(_hall.Id, _ct).Returns(_hall);
        _rehearsals.AddAsync(Arg.Do<Rehearsal>(_added.Add), _ct).Returns(Task.CompletedTask);
        _songs.ListAsync(Arg.Any<Expression<Func<Song, bool>>>(), _ct)
            .Returns(call => new[] { _kyrie, _gloria, _retired }.AsQueryable()
                .Where(call.Arg<Expression<Func<Song, bool>>>()).ToList());
    }

    private CreateRehearsalRequest NewRequest(params (Guid SongId, string? Note)[] songs) => new()
    {
        EventId = _event.Id, LocationId = _hall.Id, StartTime = Start, EndTime = Start.AddHours(2), Note = "Bring folders",
        Songs = songs.Select(s => new UpdateRehearsalSongRequest { SongId = s.SongId, Note = s.Note }).ToList(),
    };

    [Fact]
    public async Task Create_Valid_StoresRehearsalWithSongsInRequestOrder_Async()
    {
        var result = await _sut.CreateAsync(NewRequest((_gloria.Id, "  slower  "), (_kyrie.Id, " ")), _ct);

        Assert.True(result.IsSuccess);
        var rehearsal = Assert.Single(_added);
        Assert.Equal((_event.Id, _hall.Id, Start, Start.AddHours(2)),
            (rehearsal.EventId, rehearsal.LocationId, rehearsal.StartTime, rehearsal.EndTime));
        await _rehearsals.Received(1).SaveChangesAsync(_ct);

        var dto = result.Value!;
        Assert.Equal(rehearsal.Id, dto.Id);
        Assert.Equal("Parish hall", dto.LocationName);
        Assert.Equal("Bring folders", dto.Note);
        Assert.Equal(
            [(_gloria.Id, 1, "slower"), (_kyrie.Id, 2, null)],
            dto.Songs.Select(s => (s.SongId, s.DisplayOrder, s.Note)));
    }

    [Fact]
    public async Task Create_WithoutEventLocationOrSongs_Succeeds_AndSkipsConflictCheck_Async()
    {
        var request = new CreateRehearsalRequest { StartTime = Start, EndTime = Start.AddHours(1) };

        var result = await _sut.CreateAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.LocationName);
        Assert.Empty(result.Value.Songs);
        await _rehearsals.DidNotReceive().HasConflictAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<Guid?>(), _ct);
    }

    [Fact]
    public async Task Create_UnknownEvent_ReturnsEventNotFound_Async()
    {
        var request = NewRequest();
        request.EventId = Guid.NewGuid();

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, (await _sut.CreateAsync(request, _ct)).Code);
    }

    [Fact]
    public async Task Create_CancelledEvent_ReturnsEventCancelled_Async()
    {
        _event.Status = EventStatus.Cancelled;

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, (await _sut.CreateAsync(NewRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Create_UnknownLocation_ReturnsLookupNotFound_Async()
    {
        var request = NewRequest();
        request.LocationId = Guid.NewGuid();

        Assert.Equal(ErrorCodes.LookupNotFound, (await _sut.CreateAsync(request, _ct)).Code);
    }

    [Fact]
    public async Task Create_InactiveLocation_ReturnsLookupInactive_Async()
    {
        _hall.IsActive = false;

        Assert.Equal(ErrorCodes.LookupInactive, (await _sut.CreateAsync(NewRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Create_LocationBusyAtThatTime_ReturnsTimeConflict_Async()
    {
        _rehearsals.HasConflictAsync(_hall.Id, Start, Start.AddHours(2), null, _ct).Returns(true);

        var result = await _sut.CreateAsync(NewRequest(), _ct);

        Assert.Equal(ErrorCodes.RehearsalTimeConflict, result.Code);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task Create_UnknownSong_ReturnsSongNotFound_Async()
    {
        var result = await _sut.CreateAsync(NewRequest((_kyrie.Id, null), (Guid.NewGuid(), null)), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task Create_InactiveSong_ReturnsSongInactive_Async()
    {
        var result = await _sut.CreateAsync(NewRequest((_retired.Id, null)), _ct);

        Assert.Equal(ErrorCodes.SongInactive, result.Code);
        await _rehearsals.DidNotReceive().SaveChangesAsync(_ct);
    }

    // ---- Update / Delete (B20) ----

    private Rehearsal Existing(DateTime start, Guid? locationId = null) => new()
    {
        Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(2), LocationId = locationId,
    };

    private UpdateRehearsalRequest UpdateRequest() => new()
    {
        EventId = _event.Id, LocationId = _hall.Id, StartTime = Start, EndTime = Start.AddHours(2), Note = "Moved",
    };

    [Fact]
    public async Task Update_Valid_SavesChecksConflictWithoutItselfAndNotifiesMembers_Async()
    {
        var rehearsal = Existing(Start.AddDays(1));
        _rehearsals.GetWithDetailsForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);
        var recipients = new List<Guid> { Guid.NewGuid() };
        _users.GetActiveUserIdsByRolesAsync(
                Arg.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { RoleNames.ChoirMember })), _ct)
            .Returns(recipients);

        var result = await _sut.UpdateAsync(rehearsal.Id, UpdateRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal((_hall.Id, Start, "Moved"), (rehearsal.LocationId, rehearsal.StartTime, rehearsal.Note));
        Assert.Equal("Parish hall", result.Value!.LocationName);
        await _rehearsals.Received(1).HasConflictAsync(_hall.Id, Start, Start.AddHours(2), rehearsal.Id, _ct);
        await _rehearsals.Received(1).SaveChangesAsync(_ct);
        // Start is 12:00 UTC, i.e. 19:00 at the parish.
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.RehearsalChanged
                && x.ReferenceId == rehearsal.Id && x.RecipientUserIds == recipients
                && x.Content.Contains("19:00 01/12/2026")),
            _ct);
    }

    [Fact]
    public async Task Update_KeepsLocationRetiredSinceItWasChosen_Async()
    {
        var rehearsal = Existing(Start.AddDays(1), _hall.Id);
        _rehearsals.GetWithDetailsForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);
        _hall.IsActive = false;

        Assert.True((await _sut.UpdateAsync(rehearsal.Id, UpdateRequest(), _ct)).IsSuccess);
    }

    [Fact]
    public async Task Update_Ended_ReturnsAlreadyPassed_Async()
    {
        var rehearsal = Existing(DateTime.UtcNow.AddHours(-3));
        _rehearsals.GetWithDetailsForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);

        Assert.Equal(ErrorCodes.RehearsalAlreadyPassed, (await _sut.UpdateAsync(rehearsal.Id, UpdateRequest(), _ct)).Code);
        await _rehearsals.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound_Async()
    {
        Assert.Equal(ErrorCodes.RehearsalNotFound, (await _sut.UpdateAsync(Guid.NewGuid(), UpdateRequest(), _ct)).Code);
    }

    [Fact]
    public async Task Delete_WithAttendance_ReturnsHasAttendance_Async()
    {
        var rehearsal = Existing(Start);
        rehearsal.Attendances.Add(new RehearsalAttendance { Id = Guid.NewGuid() });
        _rehearsals.GetWithAttendancesForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);

        Assert.Equal(ErrorCodes.RehearsalHasAttendance, (await _sut.DeleteAsync(rehearsal.Id, _ct)).Code);
        _rehearsals.DidNotReceive().Remove(Arg.Any<Rehearsal>());
    }

    [Fact]
    public async Task Delete_Upcoming_RemovesAndNotifiesMembers_Async()
    {
        var rehearsal = Existing(Start);
        _rehearsals.GetWithAttendancesForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);

        Assert.True((await _sut.DeleteAsync(rehearsal.Id, _ct)).IsSuccess);
        _rehearsals.Received(1).Remove(rehearsal);
        await _rehearsals.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.RehearsalChanged), _ct);
    }

    [Fact]
    public async Task Delete_Ended_RemovesWithoutNotification_Async()
    {
        var rehearsal = Existing(DateTime.UtcNow.AddHours(-3));
        _rehearsals.GetWithAttendancesForUpdateAsync(rehearsal.Id, _ct).Returns(rehearsal);

        Assert.True((await _sut.DeleteAsync(rehearsal.Id, _ct)).IsSuccess);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound_Async()
    {
        Assert.Equal(ErrorCodes.RehearsalNotFound, (await _sut.DeleteAsync(Guid.NewGuid(), _ct)).Code);
    }
}

using AutoMapper;
using Harmonia.Application.DTOs;
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
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
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
                    cfg.AddProfile<EventParticipationProfile>();
                    cfg.AddProfile<RosterAssignmentProfile>();
                },
                NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new UpcomingScheduleService(_events, _rehearsals, _members, ChoirScopeFakes.Unrestricted(), mapper);
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
        _rehearsals.GetUpcomingAsync(Arg.Do<DateTime>(fromTimes.Add), null, _ct).Returns([withLocation, withoutLocation]);
        var before = DateTime.UtcNow;

        var result = await _sut.GetUpcomingRehearsalsAsync(Guid.NewGuid(), new GetScheduleRehearsalsRequest(), _ct);

        Assert.InRange(Assert.Single(fromTimes), before, DateTime.UtcNow);
        Assert.True(result.IsSuccess);
        Assert.Equal([withLocation.Id, withoutLocation.Id], result.Value!.Select(r => r.Id));
        var dto = result.Value![0];
        Assert.Equal((start, start.AddHours(2), "Main church", "Bring folders"), (dto.StartTime, dto.EndTime, dto.LocationName, dto.Note));
        Assert.Equal([gloria.Id, kyrie.Id], dto.Songs.Select(s => s.SongId));
        Assert.Null(result.Value[1].LocationName);
        Assert.Empty(result.Value[1].Songs);
    }

    [Fact]
    public async Task GetUpcomingRehearsals_WithDateRange_AsksThatRangeInsteadOfUpcoming_Async()
    {
        var start = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var past = new Rehearsal { Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(1) };
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);
        _rehearsals.GetStartingBetweenAsync(from, to, null, _ct).Returns([past]);

        var result = await _sut.GetUpcomingRehearsalsAsync(
            Guid.NewGuid(), new GetScheduleRehearsalsRequest { FromDate = from, ToDate = to }, _ct);

        Assert.Equal([past.Id], result.Value!.Select(r => r.Id));
        await _rehearsals.DidNotReceive().GetUpcomingAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<Guid>?>(), _ct);
    }

    // ---- Own sections for members ----

    private MemberProfile StubMember()
    {
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _members.GetByUserIdAsync(member.UserId, _ct).Returns(member);
        return member;
    }

    private static RosterAssignment Line(string skill, string? song = null)
    {
        var item = song is null ? null : new SongListItem { Id = Guid.NewGuid(), Song = new Song { Title = song } };
        return new RosterAssignment
        {
            Id = Guid.NewGuid(), SkillId = Guid.NewGuid(), Skill = new Skill { Name = skill },
            SongListItemId = item?.Id, SongListItem = item, NotifiedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task GetMyAssignments_GroupsTheLinesUnderEachEvent_Async()
    {
        var member = StubMember();
        var alto = Line("Alto", "Gloria");
        var christmas = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = new DateOnly(2026, 12, 25), Time = new TimeOnly(8, 0), Title = "Christmas",
            Location = _church,
            ServiceRoster = new ServiceRoster { Status = RosterStatus.Finalized, Assignments = [alto, Line("Guitar")] },
        };
        var fromDates = new List<DateOnly>();
        var before = VietnamTime.Today;
        _events.GetUpcomingAssignedToMemberAsync(member.Id, Arg.Do<DateOnly>(fromDates.Add), _ct).Returns([christmas]);

        var result = await _sut.GetMyAssignmentsAsync(member.UserId, _ct);

        Assert.InRange(Assert.Single(fromDates), before, VietnamTime.Today);
        var dto = Assert.Single(result.Value!);
        Assert.Equal((christmas.Id, "Christmas", "Main church"), (dto.EventId, dto.Title, dto.LocationName));
        Assert.Equal(["Alto", "Guitar"], dto.Assignments.Select(a => a.SkillName));
        Assert.Equal(
            (alto.SkillId, (Guid?)alto.SongListItem!.Id, "Gloria"),
            (dto.Assignments[0].SkillId, dto.Assignments[0].SongListItemId, dto.Assignments[0].SongTitle));
        Assert.Null(dto.Assignments[1].SongTitle);
    }

    [Fact]
    public async Task GetMyAssignments_UserWithoutMemberProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.GetMyAssignmentsAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
    }

    [Fact]
    public async Task GetEventDetail_FillsTheCallersRequestAssignmentsAndAttendance_Async()
    {
        var member = StubMember();
        var start = new DateTime(2026, 12, 20, 12, 0, 0, DateTimeKind.Utc);
        var held = new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(2),
            Attendances = [new RehearsalAttendance { MemberId = member.Id, Status = AttendanceStatus.Late }],
        };
        var next = new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = start.AddDays(2), EndTime = start.AddDays(2).AddHours(2), Location = _church,
        };
        var christmas = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Time = new TimeOnly(8, 0),
            Status = EventStatus.Published, Location = _church, SpecialRequirements = "White robes",
            MassType = new MassType { Name = "Solemn" }, Rehearsals = [next, held],
            ServiceRoster = new ServiceRoster { Status = RosterStatus.Finalized, Assignments = [Line("Alto")] },
        };
        christmas.EventParticipations =
        [
            new EventParticipation
            {
                Id = Guid.NewGuid(), EventId = christmas.Id, MemberId = member.Id, Status = ParticipationStatus.Invited,
                LiturgicalEvent = christmas,
            },
        ];
        _events.GetPublishedForMemberAsync(christmas.Id, member.Id, _ct).Returns(christmas);

        var result = await _sut.GetEventDetailAsync(member.UserId, christmas.Id, _ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(
            ("Main church", "Solemn", "White robes", (string?)null),
            (dto.LocationName, dto.MassTypeName, dto.SpecialRequirements, dto.LiturgicalSeasonName));
        Assert.Equal((ParticipationStatus.Invited, true), (dto.Participation!.Status, dto.Participation.CanRespond));
        Assert.Equal("Alto", Assert.Single(dto.Assignments).SkillName);
        Assert.Equal([held.Id, next.Id], dto.Rehearsals.Select(r => r.Id));
        Assert.Equal([AttendanceStatus.Late, null], dto.Rehearsals.Select(r => r.MyAttendanceStatus));
    }

    [Fact]
    public async Task GetEventDetail_InactiveMember_CannotRespond_Async()
    {
        var member = StubMember();
        member.Status = MemberStatus.Inactive;
        var christmas = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Time = new TimeOnly(8, 0),
            Status = EventStatus.Published, Location = _church,
        };
        christmas.EventParticipations =
        [
            new EventParticipation
            {
                Id = Guid.NewGuid(), EventId = christmas.Id, MemberId = member.Id, Status = ParticipationStatus.Invited,
                LiturgicalEvent = christmas,
            },
        ];
        _events.GetPublishedForMemberAsync(christmas.Id, member.Id, _ct).Returns(christmas);

        var result = await _sut.GetEventDetailAsync(member.UserId, christmas.Id, _ct);

        Assert.Equal(
            (false, ErrorCodes.MemberNotActive),
            (result.Value!.Participation!.CanRespond, result.Value.Participation.CannotRespondReason));
    }

    [Fact]
    public async Task GetEventDetail_NotMember_AsksWithEmptyMemberId_AndGetsNoOwnSections_Async()
    {
        var christmas = new LiturgicalEvent { Id = Guid.NewGuid(), Status = EventStatus.Published, Location = _church };
        _events.GetPublishedForMemberAsync(christmas.Id, Guid.Empty, _ct).Returns(christmas);

        var result = await _sut.GetEventDetailAsync(Guid.NewGuid(), christmas.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Participation);
        Assert.Empty(result.Value.Assignments);
    }

    [Fact]
    public async Task GetEventDetail_NotPublishedOrMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetEventDetailAsync(Guid.NewGuid(), Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Fact]
    public async Task GetRehearsalDetail_MapsTheCallersAttendance_Async()
    {
        var member = StubMember();
        var start = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(2), Location = _church, Note = "Bring folders",
            Attendances = [new RehearsalAttendance { MemberId = member.Id, Status = AttendanceStatus.Excused }],
        };
        _rehearsals.GetVisibleForMemberAsync(rehearsal.Id, member.Id, Arg.Any<IReadOnlyCollection<Guid>?>(), _ct).Returns(rehearsal);

        var result = await _sut.GetRehearsalDetailAsync(member.UserId, rehearsal.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ("Main church", "Bring folders", (AttendanceStatus?)AttendanceStatus.Excused),
            (result.Value!.LocationName, result.Value.Note, result.Value.MyAttendanceStatus));
    }

    [Fact]
    public async Task GetRehearsalDetail_HiddenOrMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetRehearsalDetailAsync(Guid.NewGuid(), Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }
}

using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class MemberProfileServiceTests
{
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IPracticeAssignmentRepository _assignments = Substitute.For<IPracticeAssignmentRepository>();
    private readonly MemberProfileService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly MemberProfile _member = new() { Id = Guid.NewGuid(), Status = MemberStatus.Active };
    private readonly SearchMyParticipationHistoryRequest _request = new();

    public MemberProfileServiceTests()
    {
        _sut = new MemberProfileService(_members, _events, _assignments, Substitute.For<IMapper>());
        _members.GetByUserIdAsync(_userId, _ct).Returns(_member);
    }

    private void HistoryPage(params LiturgicalEvent[] events) =>
        _events.GetHistoryForMemberAsync(_member.Id, _request, Arg.Any<DateOnly>(), _ct)
            .Returns(new PagedList<LiturgicalEvent>(events, 1, 20, events.Length));

    private static Rehearsal HeldRehearsal(params AttendanceStatus[] statuses) => new()
    {
        StartTime = DateTime.UtcNow.AddDays(-10),
        Attendances = statuses.Select(s => new RehearsalAttendance { Status = s }).ToList(),
    };

    [Fact]
    public async Task GetMyHistory_BuildsRowPerEvent_Async()
    {
        var tenor = new Skill { Name = "Tenor" };
        var served = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), Title = "Christmas", LiturgicalSeason = new LiturgicalSeason { Name = "Christmas" },
            EventParticipations = [new EventParticipation { Status = ParticipationStatus.Confirmed }],
            ServiceRoster = new ServiceRoster
            {
                Status = RosterStatus.Finalized,
                Assignments = [new RosterAssignment { Skill = tenor }, new RosterAssignment { Skill = tenor }],
            },
            Rehearsals = [HeldRehearsal(AttendanceStatus.Late), HeldRehearsal(AttendanceStatus.Absent), HeldRehearsal()],
        };
        var declined = new LiturgicalEvent
        {
            Id = Guid.NewGuid(),
            EventParticipations = [new EventParticipation { Status = ParticipationStatus.Declined }],
            // Still a draft roster: listed but not served.
            ServiceRoster = new ServiceRoster { Status = RosterStatus.Draft, Assignments = [new RosterAssignment { Skill = tenor }] },
        };
        HistoryPage(served, declined);
        _assignments.GetForMemberByEventsAsync(_member.Id, Arg.Any<IReadOnlyCollection<Guid>>(), _ct).Returns([
            new PracticeAssignment { EventId = served.Id, Submissions = [new PracticeSubmission { Status = SubmissionStatus.Passed }] },
            new PracticeAssignment { EventId = served.Id, Submissions = [new PracticeSubmission { Status = SubmissionStatus.NeedsRevision }] },
            new PracticeAssignment { EventId = served.Id },
        ]);

        var result = await _sut.GetMyHistoryAsync(_userId, _request, _ct);

        Assert.True(result.IsSuccess);
        var (first, second) = (result.Value!.Items[0], result.Value.Items[1]);
        Assert.Equal("Christmas", first.LiturgicalSeasonName);
        Assert.Equal(ParticipationStatus.Confirmed, first.ParticipationStatus);
        Assert.Equal(["Tenor"], first.ServedSkills);
        Assert.Equal((3, 1), (first.RehearsalsHeld, first.RehearsalsAttended));
        Assert.Equal((3, 1), (first.AssignmentsTotal, first.AssignmentsPassed));
        Assert.Equal(ParticipationStatus.Declined, second.ParticipationStatus);
        Assert.Empty(second.ServedSkills);
        Assert.Equal((0, 0), (second.AssignmentsTotal, second.RehearsalsHeld));
    }

    [Fact]
    public async Task GetMyHistory_QueriesOnlyTheCallersOwnRecords_Async()
    {
        HistoryPage();

        await _sut.GetMyHistoryAsync(_userId, _request, _ct);

        // The member id always comes from the caller's profile, never from the request.
        await _events.Received(1).GetHistoryForMemberAsync(_member.Id, _request, VietnamTime.Today, _ct);
        await _assignments.Received(1).GetForMemberByEventsAsync(_member.Id, Arg.Any<IReadOnlyCollection<Guid>>(), _ct);
    }

    [Fact]
    public async Task GetMyHistory_NoMemberProfile_ReturnsNotFound_Async()
    {
        var result = await _sut.GetMyHistoryAsync(Guid.NewGuid(), _request, _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _events.DidNotReceiveWithAnyArgs().GetHistoryForMemberAsync(default, null!, default, default);
    }
}

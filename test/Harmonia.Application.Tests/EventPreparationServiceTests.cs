using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class EventPreparationServiceTests
{
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly IPracticeAssignmentRepository _assignments = Substitute.For<IPracticeAssignmentRepository>();
    private readonly EventPreparationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _tenorId = Guid.NewGuid();
    private readonly MemberProfile _alice;
    private readonly MemberProfile _bob;

    public EventPreparationServiceTests()
    {
        _sut = new EventPreparationService(_events, _members, _rehearsals, _assignments);

        _alice = new MemberProfile { Id = Guid.NewGuid(), User = new User { FullName = "Alice" } };
        _bob = new MemberProfile
        {
            Id = Guid.NewGuid(), User = new User { FullName = "Bob" },
            MemberSkills = [new MemberSkill { SkillId = _tenorId, Status = ApprovalStatus.Approved }],
            EventParticipations = [new EventParticipation { EventId = _eventId, Status = ParticipationStatus.Confirmed }],
        };

        _events.GetByIdAsync(_eventId, _ct).Returns(new LiturgicalEvent { Id = _eventId });
        _members.GetActiveForEventAsync(_eventId, _ct).Returns([_alice, _bob]);
        _rehearsals.GetByEventWithAttendancesAsync(_eventId, _ct).Returns([]);
        _assignments.GetByEventWithSubmissionsAsync(_eventId, _ct).Returns([]);
    }

    private static Rehearsal RehearsalAt(DateTime start, params (Guid MemberId, AttendanceStatus Status)[] attendances) => new()
    {
        Id = Guid.NewGuid(), StartTime = start, EndTime = start.AddHours(2),
        Attendances = attendances.Select(a => new RehearsalAttendance { MemberId = a.MemberId, Status = a.Status }).ToList(),
    };

    private static PracticeSubmission Submission(Guid memberId, int attempt, SubmissionStatus status) =>
        new() { MemberId = memberId, AttemptNo = attempt, Status = status };

    [Fact]
    public async Task GetProgress_CountsHeldRehearsalsOnly_AndPresentOrLateAsAttended_Async()
    {
        var past = DateTime.UtcNow.AddDays(-1);
        _rehearsals.GetByEventWithAttendancesAsync(_eventId, _ct).Returns([
            RehearsalAt(past, (_alice.Id, AttendanceStatus.Present), (_bob.Id, AttendanceStatus.Absent)),
            RehearsalAt(past.AddHours(-5), (_alice.Id, AttendanceStatus.Late), (_bob.Id, AttendanceStatus.Excused)),
            RehearsalAt(DateTime.UtcNow.AddDays(1), (_alice.Id, AttendanceStatus.Present)),
        ]);

        var result = await _sut.GetProgressAsync(_eventId, _ct);

        Assert.True(result.IsSuccess);
        var (alice, bob) = (result.Value![0], result.Value[1]);
        Assert.Equal((2, 2), (alice.RehearsalsHeld, alice.RehearsalsAttended));
        Assert.Equal((2, 0), (bob.RehearsalsHeld, bob.RehearsalsAttended));
        Assert.Null(alice.ParticipationStatus);
        Assert.Equal(ParticipationStatus.Confirmed, bob.ParticipationStatus);
    }

    [Fact]
    public async Task GetProgress_AppliesReceivingRule_AndLatestAttempt_Async()
    {
        var pastDue = DateTime.UtcNow.AddDays(-1);
        _assignments.GetByEventWithSubmissionsAsync(_eventId, _ct).Returns([
            // Everyone; Alice passed on the second attempt, Bob never submitted and it is past due.
            new PracticeAssignment
            {
                Scope = AssignmentScope.All, DueDate = pastDue,
                Submissions = [Submission(_alice.Id, 1, SubmissionStatus.NeedsRevision), Submission(_alice.Id, 2, SubmissionStatus.Passed)],
            },
            // Tenor only: Bob receives it, Alice does not.
            new PracticeAssignment
            {
                Scope = AssignmentScope.SkillGroup, DueDate = DateTime.UtcNow.AddDays(3),
                Targets = [new PracticeAssignmentTarget { TargetType = TargetType.Skill, SkillId = _tenorId }],
            },
            // Named member: Alice; her newest attempt needs revision and it is past due.
            new PracticeAssignment
            {
                Scope = AssignmentScope.Individual, DueDate = pastDue,
                Targets = [new PracticeAssignmentTarget { TargetType = TargetType.Member, MemberId = _alice.Id }],
                Submissions = [Submission(_alice.Id, 2, SubmissionStatus.NeedsRevision), Submission(_alice.Id, 1, SubmissionStatus.Passed)],
            },
        ]);

        var result = await _sut.GetProgressAsync(_eventId, _ct);

        var (alice, bob) = (result.Value![0], result.Value[1]);
        Assert.Equal((2, 1, 1), (alice.AssignmentsTotal, alice.AssignmentsPassed, alice.AssignmentsOverdue));
        Assert.Equal((2, 0, 1), (bob.AssignmentsTotal, bob.AssignmentsPassed, bob.AssignmentsOverdue));
    }

    [Fact]
    public async Task GetProgress_EventMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetProgressAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
        await _members.DidNotReceive().GetActiveForEventAsync(Arg.Any<Guid>(), _ct);
    }
}

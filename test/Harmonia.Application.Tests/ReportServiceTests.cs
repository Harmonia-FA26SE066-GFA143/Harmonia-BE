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
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IPracticeAssignmentRepository _assignments = Substitute.For<IPracticeAssignmentRepository>();
    private readonly ReportService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly SearchReportsRequest _request = new();

    public ReportServiceTests() => _sut = new ReportService(_events, _songs, _users, _members, _assignments);

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

    // ---- Admin reports (UC-34) ----

    private MemberProfile ReturnsMember(MemberProfile member)
    {
        member.User = new User { FullName = "Alice" };
        _members.GetActiveWithActivityAsync(_request, _ct).Returns(new PagedList<MemberProfile>([member], 1, 20, 1));
        return member;
    }

    [Fact]
    public async Task GetUserActivity_MapsUserAndCounts_Async()
    {
        var lastLogin = DateTime.UtcNow.AddHours(-1);
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "Alice", Email = "a@test.com", IsActive = true, LastLoginAt = lastLogin,
            Role = new Role { Name = "ChoirMember" },
        };
        _users.GetActivityAsync(_request, _ct)
            .Returns(new PagedList<(User, int, int, int, int)>([(user, 1, 2, 3, 4)], 1, 20, 1));

        var row = Assert.Single((await _sut.GetUserActivityAsync(_request, _ct)).Value!.Items);

        Assert.Equal(
            (user.Id, "Alice", "a@test.com", "ChoirMember", true, lastLogin),
            (row.UserId, row.FullName, row.Email, row.RoleName, row.IsActive, row.LastLoginAt!.Value));
        Assert.Equal((1, 2, 3, 4), (row.PasswordChangeCount, row.PasswordResetCount, row.AccountChangeCount, row.ActionCount));
    }

    [Fact]
    public async Task GetRehearsalAttendance_CountsEachStatus_Async()
    {
        var member = ReturnsMember(new MemberProfile
        {
            Id = Guid.NewGuid(),
            RehearsalAttendances = new[]
            {
                AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Late, AttendanceStatus.Absent,
            }.Select(s => new RehearsalAttendance { Status = s }).ToList(),
        });

        var row = Assert.Single((await _sut.GetRehearsalAttendanceAsync(_request, _ct)).Value!.Items);

        Assert.Equal((member.Id, "Alice"), (row.MemberId, row.FullName));
        Assert.Equal((2, 1, 1, 0), (row.PresentCount, row.LateCount, row.AbsentCount, row.ExcusedCount));
    }

    [Fact]
    public async Task GetParticipation_CountsEachStatus_Async()
    {
        ReturnsMember(new MemberProfile
        {
            Id = Guid.NewGuid(),
            EventParticipations = new[]
            {
                ParticipationStatus.Confirmed, ParticipationStatus.Confirmed, ParticipationStatus.Declined, ParticipationStatus.Invited,
            }.Select(s => new EventParticipation { Status = s }).ToList(),
        });

        var row = Assert.Single((await _sut.GetParticipationAsync(_request, _ct)).Value!.Items);

        Assert.Equal((1, 2, 1, 0), (row.InvitedCount, row.ConfirmedCount, row.DeclinedCount, row.UnsureCount));
    }

    [Fact]
    public async Task GetAssignmentCompletion_CountsReceivedAssignmentsByNewestAttempt_Async()
    {
        var tenor = Guid.NewGuid();
        var member = ReturnsMember(new MemberProfile { Id = Guid.NewGuid(), MemberSkills = [new MemberSkill { SkillId = tenor }] });
        PracticeAssignment ForAll(params SubmissionStatus[] attempts) => new()
        {
            Scope = AssignmentScope.All,
            Submissions = attempts.Select((status, i) => new PracticeSubmission
            {
                MemberId = member.Id, AttemptNo = i + 1, Status = status,
            }).ToList(),
        };
        var forTenors = ForAll(SubmissionStatus.Submitted);
        forTenors.Scope = AssignmentScope.SkillGroup;
        forTenors.Targets = [new PracticeAssignmentTarget { TargetType = TargetType.Skill, SkillId = tenor }];
        var forSomeoneElse = ForAll();
        forSomeoneElse.Scope = AssignmentScope.Individual;
        forSomeoneElse.Targets = [new PracticeAssignmentTarget { TargetType = TargetType.Member, MemberId = Guid.NewGuid() }];
        _assignments.GetForReportAsync(
                _request, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { member.Id })), _ct)
            .Returns(
            [
                ForAll(SubmissionStatus.NeedsRevision, SubmissionStatus.Passed),
                ForAll(SubmissionStatus.Passed, SubmissionStatus.NeedsRevision),
                ForAll(),
                forTenors,
                forSomeoneElse,
            ]);

        var row = Assert.Single((await _sut.GetAssignmentCompletionAsync(_request, _ct)).Value!.Items);

        Assert.Equal(
            (4, 1, 1, 1, 1),
            (row.AssignedCount, row.PassedCount, row.NeedsRevisionCount, row.AwaitingReviewCount, row.NotSubmittedCount));
    }
}

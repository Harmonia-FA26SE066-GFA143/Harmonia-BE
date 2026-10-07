using System.Linq.Expressions;
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

public class PracticeAssignmentServiceTests
{
    private readonly IPracticeAssignmentRepository _assignments = Substitute.For<IPracticeAssignmentRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IMusicMaterialRepository _materials = Substitute.For<IMusicMaterialRepository>();
    private readonly IGenericRepository<Skill> _skills = Substitute.For<IGenericRepository<Skill>>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IMemberSkillRepository _memberSkills = Substitute.For<IMemberSkillRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly PracticeAssignmentService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly MemberProfile _activeMember = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = MemberStatus.Active };
    private readonly MemberProfile _leftMember = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = MemberStatus.Left };
    private readonly Skill _soprano = new() { Id = Guid.NewGuid(), Name = "Soprano" };

    public PracticeAssignmentServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<PracticeAssignmentProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new PracticeAssignmentService(
            _assignments, _events, _songs, _materials, _skills, _members, _memberSkills, _notifications, mapper);

        // Run the service's predicate over in-memory rows, so the Active filter is exercised too.
        MemberProfile[] members = [_activeMember, _leftMember];
        _members.ListAsync(Arg.Any<Expression<Func<MemberProfile, bool>>>(), _ct)
            .Returns(ci => members.Where(ci.Arg<Expression<Func<MemberProfile, bool>>>().Compile()).ToList());
        _skills.ListAsync(Arg.Any<Expression<Func<Skill, bool>>>(), _ct)
            .Returns(ci => new[] { _soprano }.Where(ci.Arg<Expression<Func<Skill, bool>>>().Compile()).ToList());
    }

    private static CreatePracticeAssignmentRequest Request(AssignmentScope scope) => new()
    {
        Title = "Learn the entrance hymn",
        Scope = scope,
        DueDate = DateTime.UtcNow.AddDays(3),
    };

    private async Task AssertNothingSavedAsync()
    {
        await _assignments.DidNotReceive().AddAsync(Arg.Any<PracticeAssignment>(), Arg.Any<CancellationToken>());
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AllScope_SavesWithoutTargetsAndNotifiesActiveMembers_Async()
    {
        var result = await _sut.CreateAsync(Request(AssignmentScope.All), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.SkillIds);
        Assert.Empty(result.Value.MemberIds);
        await _assignments.Received(1).AddAsync(Arg.Is<PracticeAssignment>(x => x.Targets.Count == 0), _ct);
        await _assignments.Received(1).SaveChangesAsync(_ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x =>
                x.Type == NotificationType.AssignmentNotice
                && x.ReferenceType == nameof(PracticeAssignment)
                && x.ReferenceId == result.Value.Id
                && x.RecipientUserIds.SequenceEqual(new[] { _activeMember.UserId })),
            _ct);
    }

    [Fact]
    public async Task Create_SkillGroupScope_SavesSkillTargetsAndNotifiesSkillHolders_Async()
    {
        var holderUserId = Guid.NewGuid();
        _memberSkills.GetActiveMemberUserIdsBySkillsAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(new[] { _soprano.Id })), _ct)
            .Returns([holderUserId]);
        var request = Request(AssignmentScope.SkillGroup);
        request.SkillIds = [_soprano.Id, _soprano.Id];

        var result = await _sut.CreateAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([_soprano.Id], result.Value!.SkillIds);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.RecipientUserIds.SequenceEqual(new[] { holderUserId })), _ct);
    }

    [Fact]
    public async Task Create_IndividualScope_SavesMemberTargetsAndNotifiesThem_Async()
    {
        var request = Request(AssignmentScope.Individual);
        request.MemberIds = [_activeMember.Id];

        var result = await _sut.CreateAsync(request, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([_activeMember.Id], result.Value!.MemberIds);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.RecipientUserIds.SequenceEqual(new[] { _activeMember.UserId })), _ct);
    }

    [Fact]
    public async Task Create_UnknownSkill_ReturnsSkillNotFound_Async()
    {
        var request = Request(AssignmentScope.SkillGroup);
        request.SkillIds = [_soprano.Id, Guid.NewGuid()];

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_InactiveSkill_ReturnsSkillInactive_Async()
    {
        _soprano.IsActive = false;
        var request = Request(AssignmentScope.SkillGroup);
        request.SkillIds = [_soprano.Id];

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.SkillInactive, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_MemberNotActive_ReturnsMemberNotFound_Async()
    {
        var request = Request(AssignmentScope.Individual);
        request.MemberIds = [_activeMember.Id, _leftMember.Id];

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_UnknownEvent_ReturnsEventNotFound_Async()
    {
        var request = Request(AssignmentScope.All);
        request.EventId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_CancelledEvent_ReturnsEventCancelled_Async()
    {
        var cancelled = new LiturgicalEvent { Id = Guid.NewGuid(), Status = EventStatus.Cancelled };
        _events.GetByIdAsync(cancelled.Id, _ct).Returns(cancelled);
        var request = Request(AssignmentScope.All);
        request.EventId = cancelled.Id;

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.EventCancelled, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_UnknownSong_ReturnsSongNotFound_Async()
    {
        var request = Request(AssignmentScope.All);
        request.SongId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        await AssertNothingSavedAsync();
    }

    [Fact]
    public async Task Create_MaterialOfAnotherSong_ReturnsMaterialNotFound_Async()
    {
        var song = new Song { Id = Guid.NewGuid(), Title = "Song" };
        var material = new MusicMaterial { Id = Guid.NewGuid(), SongId = Guid.NewGuid() };
        _songs.GetByIdAsync(song.Id, _ct).Returns(song);
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);
        var request = Request(AssignmentScope.All);
        request.SongId = song.Id;
        request.MaterialId = material.Id;

        var result = await _sut.CreateAsync(request, _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
        await AssertNothingSavedAsync();
    }

    // ---- Member's own assignments (UC-09) ----

    [Fact]
    public async Task GetMine_ReturnsMemberAssignmentsWithNewestSubmission_Async()
    {
        _members.GetByUserIdAsync(_activeMember.UserId, _ct).Returns(_activeMember);
        var request = new SearchMyPracticeAssignmentsRequest();
        var submittedAt = DateTime.UtcNow.AddHours(-1);
        var assignment = new PracticeAssignment
        {
            Id = Guid.NewGuid(),
            Title = "Learn the entrance hymn",
            Song = new Song { Title = "Entrance hymn" },
            Submissions =
            [
                new PracticeSubmission { AttemptNo = 1, Status = SubmissionStatus.NeedsRevision },
                new PracticeSubmission { AttemptNo = 2, Status = SubmissionStatus.Submitted, SubmittedAt = submittedAt },
            ],
        };
        _assignments.GetForMemberAsync(_activeMember.Id, request, _ct)
            .Returns(new PagedList<PracticeAssignment>([assignment], 1, 20, 1));

        var result = await _sut.GetMineAsync(_activeMember.UserId, request, _ct);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("Entrance hymn", item.SongTitle);
        Assert.Null(item.EventDate);
        Assert.Equal(SubmissionStatus.Submitted, item.LatestSubmissionStatus);
        Assert.Equal(submittedAt, item.LatestSubmittedAt);
    }

    [Fact]
    public async Task GetMine_NoMemberProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.GetMineAsync(Guid.NewGuid(), new SearchMyPracticeAssignmentsRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
    }

    [Fact]
    public async Task GetMineById_AssignmentNotReceived_ReturnsPracticeAssignmentNotFound_Async()
    {
        _members.GetByUserIdAsync(_activeMember.UserId, _ct).Returns(_activeMember);

        var result = await _sut.GetMineByIdAsync(_activeMember.UserId, Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.PracticeAssignmentNotFound, result.Code);
    }
}

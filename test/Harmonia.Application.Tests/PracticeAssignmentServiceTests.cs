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
    private readonly IPracticeSubmissionRepository _submissions = Substitute.For<IPracticeSubmissionRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly PracticeAssignmentService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly MemberProfile _activeMember = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = MemberStatus.Active };
    private readonly MemberProfile _leftMember = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = MemberStatus.Left };
    private readonly Skill _soprano = new() { Id = Guid.NewGuid(), Name = "Soprano" };

    public PracticeAssignmentServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<PracticeAssignmentProfile>();
                cfg.AddProfile<PracticeSubmissionProfile>();
            }, NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new PracticeAssignmentService(
            _assignments, _events, _songs, _materials, _skills, _members, _memberSkills, _submissions,
            _notifications, _storage, mapper);

        _storage.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Result<FileUploadResponse>.Success(new FileUploadResponse(StoredAudio, "unused")));
        _storage.DeleteAsync(default!, default, default).ReturnsForAnyArgs(Result.Success());
        _storage.GetSignedUrl(Arg.Any<string>()).Returns(call => $"signed:{call.Arg<string>()}");
        _submissions.TryAddAsync(Arg.Any<PracticeSubmission>(), _ct).Returns(true);

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

    // ---- Submit practice audio (UC-09 / FE-11) ----

    private const string StoredAudio = "harmonia/practice-submissions/stored.m4a";

    private static FileContent NewAudio(string fileName = "take.m4a", long length = 1024) =>
        new(new MemoryStream([1, 2, 3]), fileName, length);

    /// <summary>An assignment the active member receives, with their newest attempt when given.</summary>
    private PracticeAssignment ReceivedAssignment(PracticeSubmission? latest = null, int dueInDays = 2)
    {
        var assignment = new PracticeAssignment
        {
            Id = Guid.NewGuid(),
            DueDate = DateTime.UtcNow.AddDays(dueInDays),
            Submissions = latest is null ? [] : [latest],
        };
        _members.GetByUserIdAsync(_activeMember.UserId, _ct).Returns(_activeMember);
        _assignments.GetByIdForMemberAsync(assignment.Id, _activeMember.Id, _ct).Returns(assignment);
        return assignment;
    }

    private Task<Result<PracticeSubmissionDto>> SubmitAsync(PracticeAssignment assignment, FileContent? file) =>
        _sut.SubmitAsync(_activeMember.UserId, assignment.Id, new CreatePracticeSubmissionRequest { DurationSeconds = 95 }, file, _ct);

    private async Task AssertNothingUploadedAsync() =>
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);

    [Fact]
    public async Task Submit_FirstAttempt_UploadsPrivateAudioAndSavesAttemptOne_Async()
    {
        var assignment = ReceivedAssignment();

        var result = await SubmitAsync(assignment, NewAudio());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.AttemptNo);
        Assert.Equal(SubmissionStatus.Submitted, result.Value.Status);
        Assert.Equal(95, result.Value.DurationSeconds);
        Assert.Equal($"signed:{StoredAudio}", result.Value.AudioUrl);
        await _storage.Received(1).UploadAsync(Arg.Any<Stream>(), "take.m4a", "practice-submissions", true, _ct);
        await _submissions.Received(1).TryAddAsync(
            Arg.Is<PracticeSubmission>(x => x.MemberId == _activeMember.Id
                && x.PracticeAssignmentId == assignment.Id
                && x.AudioPublicId == StoredAudio),
            _ct);
    }

    [Fact]
    public async Task Submit_AfterNeedsRevision_SavesNextAttempt_Async()
    {
        var assignment = ReceivedAssignment(new PracticeSubmission { AttemptNo = 2, Status = SubmissionStatus.NeedsRevision });

        var result = await SubmitAsync(assignment, NewAudio());

        Assert.Equal(3, result.Value!.AttemptNo);
    }

    [Fact]
    public async Task Submit_AssignmentNotReceived_ReturnsPracticeAssignmentNotFound_Async()
    {
        _members.GetByUserIdAsync(_activeMember.UserId, _ct).Returns(_activeMember);

        var result = await _sut.SubmitAsync(
            _activeMember.UserId, Guid.NewGuid(), new CreatePracticeSubmissionRequest(), NewAudio(), _ct);

        Assert.Equal(ErrorCodes.PracticeAssignmentNotFound, result.Code);
        await AssertNothingUploadedAsync();
    }

    [Fact]
    public async Task Submit_LatestAttemptPassed_ReturnsAlreadyPassed_Async()
    {
        var assignment = ReceivedAssignment(new PracticeSubmission { AttemptNo = 1, Status = SubmissionStatus.Passed });

        var result = await SubmitAsync(assignment, NewAudio());

        Assert.Equal(ErrorCodes.PracticeSubmissionAlreadyPassed, result.Code);
        await AssertNothingUploadedAsync();
    }

    [Fact]
    public async Task Submit_PastDueDate_ReturnsPastDue_Async()
    {
        var assignment = ReceivedAssignment(dueInDays: -1);

        var result = await SubmitAsync(assignment, NewAudio());

        Assert.Equal(ErrorCodes.PracticeSubmissionPastDue, result.Code);
        await AssertNothingUploadedAsync();
    }

    [Theory]
    [InlineData(null, 0, ErrorCodes.PracticeAudioRequired)]
    [InlineData("take.m4a", 0, ErrorCodes.PracticeAudioRequired)]
    [InlineData("take.ogg", 1024, ErrorCodes.PracticeAudioTypeNotAllowed)]
    [InlineData("take.MP3.exe", 1024, ErrorCodes.PracticeAudioTypeNotAllowed)]
    [InlineData("take.wav", PracticeAssignmentService.MaxAudioSizeBytes + 1, ErrorCodes.PracticeAudioTooLarge)]
    public async Task Submit_InvalidAudio_ReturnsCode_Async(string? fileName, long length, string expectedCode)
    {
        var assignment = ReceivedAssignment();

        var result = await SubmitAsync(assignment, fileName is null ? null : NewAudio(fileName, length));

        Assert.Equal(expectedCode, result.Code);
        await AssertNothingUploadedAsync();
    }

    [Fact]
    public async Task Submit_UppercaseExtension_IsAccepted_Async()
    {
        var assignment = ReceivedAssignment();

        var result = await SubmitAsync(assignment, NewAudio("TAKE.WAV"));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Submit_ConcurrentAttempt_DeletesUploadedAudioAndReturnsConflict_Async()
    {
        var assignment = ReceivedAssignment();
        _submissions.TryAddAsync(Arg.Any<PracticeSubmission>(), _ct).Returns(false);

        var result = await SubmitAsync(assignment, NewAudio());

        Assert.Equal(ErrorCodes.PracticeSubmissionConflict, result.Code);
        await _storage.Received(1).DeleteAsync(StoredAudio, true, CancellationToken.None);
    }

    [Fact]
    public async Task Submit_SaveThrows_DeletesUploadedAudioAndRethrows_Async()
    {
        var assignment = ReceivedAssignment();
        _submissions.TryAddAsync(Arg.Any<PracticeSubmission>(), _ct)
            .Returns<bool>(_ => throw new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(assignment, NewAudio()));

        await _storage.Received(1).DeleteAsync(StoredAudio, true, CancellationToken.None);
    }
}

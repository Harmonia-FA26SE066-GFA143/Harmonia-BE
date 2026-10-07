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

public class PracticeSubmissionServiceTests
{
    private readonly IPracticeSubmissionRepository _submissions = Substitute.For<IPracticeSubmissionRepository>();
    private readonly IPracticeAssignmentRepository _assignments = Substitute.For<IPracticeAssignmentRepository>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly PracticeSubmissionService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly PracticeAssignment _assignment = new()
    {
        Id = Guid.NewGuid(), Title = "Learn the entrance hymn", DueDate = DateTime.UtcNow.AddDays(2),
    };

    private readonly PracticeSubmission _submission;

    public PracticeSubmissionServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<PracticeSubmissionProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new PracticeSubmissionService(_submissions, _assignments, _notifications, _storage, mapper);
        _storage.GetSignedUrl(Arg.Any<string>()).Returns(call => $"signed:{call.Arg<string>()}");

        _submission = new PracticeSubmission
        {
            Id = Guid.NewGuid(),
            PracticeAssignmentId = _assignment.Id,
            PracticeAssignment = _assignment,
            MemberId = Guid.NewGuid(),
            Member = new MemberProfile { UserId = Guid.NewGuid(), User = new User { FullName = "Anna", AvatarUrl = "avatar" } },
            AudioPublicId = "harmonia/practice-submissions/a.m4a",
            AttemptNo = 2,
            Status = SubmissionStatus.Submitted,
        };
    }

    private void AssertReviewView(PracticeSubmissionDetailDto dto)
    {
        Assert.Equal(_submission.Id, dto.Id);
        Assert.Equal("Anna", dto.MemberName);
        Assert.Equal("avatar", dto.MemberAvatarUrl);
        Assert.Equal(_assignment.Title, dto.AssignmentTitle);
        Assert.Equal(_assignment.DueDate, dto.AssignmentDueDate);
        Assert.Equal($"signed:{_submission.AudioPublicId}", dto.AudioUrl);
    }

    [Fact]
    public async Task GetByAssignment_ExistingAssignment_ReturnsSubmissionsWithSignedAudio_Async()
    {
        var request = new SearchPracticeSubmissionsRequest();
        _assignments.GetByIdAsync(_assignment.Id, _ct).Returns(_assignment);
        _submissions.SearchAsync(_assignment.Id, request, _ct)
            .Returns(new PagedList<PracticeSubmission>([_submission], 1, 20, 1));

        var result = await _sut.GetByAssignmentAsync(_assignment.Id, request, _ct);

        Assert.True(result.IsSuccess);
        AssertReviewView(Assert.Single(result.Value!.Items));
    }

    [Fact]
    public async Task GetByAssignment_UnknownAssignment_ReturnsPracticeAssignmentNotFound_Async()
    {
        var result = await _sut.GetByAssignmentAsync(Guid.NewGuid(), new SearchPracticeSubmissionsRequest(), _ct);

        Assert.Equal(ErrorCodes.PracticeAssignmentNotFound, result.Code);
        await _submissions.DidNotReceiveWithAnyArgs().SearchAsync(default, default!, default);
    }

    [Fact]
    public async Task Search_SearchesEveryAssignment_Async()
    {
        var request = new SearchPracticeSubmissionsRequest { Status = SubmissionStatus.Submitted };
        _submissions.SearchAsync(null, request, _ct)
            .Returns(new PagedList<PracticeSubmission>([_submission], 1, 20, 1));

        var result = await _sut.SearchAsync(request, _ct);

        AssertReviewView(Assert.Single(result.Value!.Items));
    }

    [Fact]
    public async Task GetById_ExistingSubmission_ReturnsFreshSignedAudio_Async()
    {
        _submissions.GetWithMemberAsync(_submission.Id, _ct).Returns(_submission);

        var result = await _sut.GetByIdAsync(_submission.Id, _ct);

        AssertReviewView(result.Value!);
    }

    [Fact]
    public async Task GetById_UnknownSubmission_ReturnsPracticeSubmissionNotFound_Async()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.PracticeSubmissionNotFound, result.Code);
        _storage.DidNotReceiveWithAnyArgs().GetSignedUrl(default!);
    }

    // ---- Review (FE-43, FE-44) ----

    private readonly Guid _directorId = Guid.NewGuid();

    private PracticeFeedback? _savedFeedback;

    private void ReviewableSubmission(bool isLatest = true, bool saves = true)
    {
        _submissions.GetForReviewAsync(_submission.Id, _ct).Returns(_submission);
        _submissions.IsLatestAttemptAsync(_submission, _ct).Returns(isLatest);
        _submissions.TrySaveReviewAsync(_submission, Arg.Do<PracticeFeedback>(f => _savedFeedback = f), _ct).Returns(saves);
    }

    private Task<Result<PracticeSubmissionReviewDto>> ReviewAsync(SubmissionStatus result, string? comment = null) =>
        _sut.ReviewAsync(_directorId, _submission.Id, new ReviewPracticeSubmissionRequest { Result = result, Comment = comment }, _ct);

    private async Task AssertNotReviewedAsync()
    {
        Assert.Equal(SubmissionStatus.Submitted, _submission.Status);
        await _submissions.DidNotReceiveWithAnyArgs().TrySaveReviewAsync(default!, default!, default);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Theory]
    [InlineData(SubmissionStatus.Passed, null, "Practice passed")]
    [InlineData(SubmissionStatus.NeedsRevision, "  Hold the last note longer  ", "Practice needs revision")]
    public async Task Review_LatestSubmitted_RecordsFeedbackAndNotifiesMember_Async(
        SubmissionStatus decision, string? comment, string expectedTitle)
    {
        ReviewableSubmission();

        var result = await ReviewAsync(decision, comment);

        Assert.True(result.IsSuccess);
        Assert.Equal(decision, _submission.Status);
        var feedback = Assert.IsType<PracticeFeedback>(_savedFeedback);
        Assert.Equal(_submission.Id, feedback.SubmissionId);
        Assert.Equal(decision, feedback.Result);
        Assert.Equal(_directorId, feedback.ReviewerId);
        Assert.Equal(comment?.Trim(), feedback.Comment);
        Assert.Equal(decision, result.Value!.Status);
        Assert.Equal(decision, result.Value.Feedback.Result);
        Assert.Equal(comment?.Trim(), result.Value.Feedback.Comment);
        Assert.Equal($"signed:{_submission.AudioPublicId}", result.Value.AudioUrl);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x =>
                x.Type == NotificationType.PracticeFeedback
                && x.Title == expectedTitle
                && x.ReferenceType == nameof(PracticeSubmission)
                && x.ReferenceId == _submission.Id
                && x.RecipientUserIds.SequenceEqual(new[] { _submission.Member.UserId })),
            _ct);
    }

    [Fact]
    public async Task Review_UnknownSubmission_ReturnsPracticeSubmissionNotFound_Async()
    {
        var result = await ReviewAsync(SubmissionStatus.Passed);

        Assert.Equal(ErrorCodes.PracticeSubmissionNotFound, result.Code);
        await AssertNotReviewedAsync();
    }

    [Fact]
    public async Task Review_OlderAttempt_ReturnsSuperseded_Async()
    {
        ReviewableSubmission(isLatest: false);

        var result = await ReviewAsync(SubmissionStatus.Passed);

        Assert.Equal(ErrorCodes.PracticeSubmissionSuperseded, result.Code);
        await AssertNotReviewedAsync();
    }

    [Theory]
    [InlineData(SubmissionStatus.Passed)]
    [InlineData(SubmissionStatus.NeedsRevision)]
    [InlineData(SubmissionStatus.Overdue)]
    public async Task Review_NotSubmitted_ReturnsAlreadyReviewed_Async(SubmissionStatus status)
    {
        _submission.Status = status;
        ReviewableSubmission();

        var result = await ReviewAsync(SubmissionStatus.Passed);

        Assert.Equal(ErrorCodes.PracticeSubmissionAlreadyReviewed, result.Code);
        Assert.Equal(status, _submission.Status);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Review_ConcurrentReviewWins_ReturnsAlreadyReviewedWithoutNotifying_Async()
    {
        ReviewableSubmission(saves: false);

        var result = await ReviewAsync(SubmissionStatus.Passed);

        Assert.Equal(ErrorCodes.PracticeSubmissionAlreadyReviewed, result.Code);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }
}

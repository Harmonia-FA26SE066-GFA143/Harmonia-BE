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
        _sut = new PracticeSubmissionService(_submissions, _assignments, _storage, mapper);
        _storage.GetSignedUrl(Arg.Any<string>()).Returns(call => $"signed:{call.Arg<string>()}");

        _submission = new PracticeSubmission
        {
            Id = Guid.NewGuid(),
            PracticeAssignmentId = _assignment.Id,
            PracticeAssignment = _assignment,
            MemberId = Guid.NewGuid(),
            Member = new MemberProfile { User = new User { FullName = "Anna", AvatarUrl = "avatar" } },
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
}

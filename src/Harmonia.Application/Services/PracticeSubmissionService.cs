using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class PracticeSubmissionService(
    IPracticeSubmissionRepository practiceSubmissionRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository,
    INotificationService notificationService,
    IFileStorageService fileStorageService,
    IMapper mapper) : IPracticeSubmissionService
{
    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetByAssignmentAsync(
        Guid assignmentId, SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        if (await practiceAssignmentRepository.GetByIdAsync(assignmentId, cancellationToken) is null)
            return Result<PagedList<PracticeSubmissionDetailDto>>.Failure(ErrorCodes.PracticeAssignmentNotFound);

        var page = await practiceSubmissionRepository.SearchAsync(assignmentId, request, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> SearchAsync(
        SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        var page = await practiceSubmissionRepository.SearchAsync(null, request, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PracticeSubmissionDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetWithMemberAsync(id, cancellationToken);

        return submission is null
            ? Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotFound)
            : Result<PracticeSubmissionDetailDto>.Success(ToDto<PracticeSubmissionDetailDto>(submission));
    }

    public async Task<Result<PracticeSubmissionReviewDto>> ReviewAsync(
        Guid directorUserId, Guid id, ReviewPracticeSubmissionRequest request, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetForReviewAsync(id, cancellationToken);
        if (submission is null) return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionNotFound);

        if (!await practiceSubmissionRepository.IsLatestAttemptAsync(submission, cancellationToken))
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionSuperseded);

        if (submission.Status != SubmissionStatus.Submitted)
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyReviewed);

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        var feedback = new PracticeFeedback
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            ReviewerId = directorUserId,
            Result = request.Result,
            Comment = comment,
            ReviewedAt = DateTime.UtcNow,
        };
        submission.Status = request.Result;

        // Another director reviewed the submission between our read and our save; theirs stands.
        if (!await practiceSubmissionRepository.TrySaveReviewAsync(submission, feedback, cancellationToken))
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyReviewed);

        // The comment stays out of the content: it can fill the whole 1000-character column on its own.
        var title = submission.PracticeAssignment.Title;
        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.PracticeFeedback,
                request.Result == SubmissionStatus.Passed ? "Practice passed" : "Practice needs revision",
                request.Result == SubmissionStatus.Passed
                    ? $"Your recording for \"{title}\" has passed."
                    : $"Your recording for \"{title}\" needs revision. Open it to read the feedback.",
                [submission.Member.UserId],
                nameof(PracticeSubmission),
                submission.Id),
            cancellationToken);

        var dto = ToDto<PracticeSubmissionReviewDto>(submission);
        dto.Feedback = mapper.Map<PracticeFeedbackDto>(feedback);
        return Result<PracticeSubmissionReviewDto>.Success(dto);
    }

    private TDto ToDto<TDto>(PracticeSubmission submission) where TDto : PracticeSubmissionDetailDto
    {
        var dto = mapper.Map<TDto>(submission);
        dto.AudioUrl = fileStorageService.GetSignedUrl(submission.AudioPublicId);
        return dto;
    }

    private PagedList<PracticeSubmissionDetailDto> ToDtoPage(PagedList<PracticeSubmission> page) =>
        new(page.Items.Select(ToDto<PracticeSubmissionDetailDto>).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
}

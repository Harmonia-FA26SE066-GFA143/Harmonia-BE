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
    IMemberProfileRepository memberProfileRepository,
    INotificationService notificationService,
    IFileStorageService fileStorageService,
    IChoirScopeService choirScope,
    IMapper mapper) : IPracticeSubmissionService
{
    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetByAssignmentAsync(
        Guid assignmentId, SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        var assignment = await practiceAssignmentRepository.GetByIdAsync(assignmentId, cancellationToken);
        if (assignment is null || !await choirScope.CanManageChoirAsync(assignment.ChoirId, cancellationToken))
            return Result<PagedList<PracticeSubmissionDetailDto>>.Failure(ErrorCodes.PracticeAssignmentNotFound);

        var page = await practiceSubmissionRepository.SearchAsync(assignmentId, request, null, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> SearchAsync(
        SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        var choirIds = await choirScope.GetDirectedChoirIdsAsync(cancellationToken);
        var page = await practiceSubmissionRepository.SearchAsync(null, request, choirIds, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PracticeSubmissionDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetWithMemberAsync(id, cancellationToken);
        if (submission is not null
            && !await choirScope.CanManageChoirAsync(submission.PracticeAssignment.ChoirId, cancellationToken))
        {
            submission = null;
        }

        return submission is null
            ? Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotFound)
            : Result<PracticeSubmissionDetailDto>.Success(ToDto<PracticeSubmissionDetailDto>(submission));
    }

    public async Task<Result<PracticeSubmissionReviewDto>> ReviewAsync(
        Guid directorUserId, Guid id, ReviewPracticeSubmissionRequest request, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetForReviewAsync(id, cancellationToken);
        if (submission is null
            || !await choirScope.CanManageChoirAsync(submission.PracticeAssignment.ChoirId, cancellationToken))
        {
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionNotFound);
        }

        if (!await practiceSubmissionRepository.IsLatestAttemptAsync(submission, cancellationToken))
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionSuperseded);

        if (submission.Status != SubmissionStatus.Submitted)
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyReviewed);

        submission.Status = request.Result;
        var feedback = NewFeedback(submission, directorUserId, request.Comment);

        // Another director reviewed the submission between our read and our save; theirs stands.
        if (!await practiceSubmissionRepository.TrySaveReviewAsync(submission, feedback, cancellationToken))
            return Result<PracticeSubmissionReviewDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyReviewed);

        var title = submission.PracticeAssignment.Title;
        await NotifyMemberAsync(
            submission,
            request.Result == SubmissionStatus.Passed ? "Bài tập đạt yêu cầu" : "Bài tập cần chỉnh sửa",
            request.Result == SubmissionStatus.Passed
                ? $"Bản thu \"{title}\" của bạn đã đạt yêu cầu."
                : $"Bản thu \"{title}\" của bạn cần chỉnh sửa. Mở để xem nhận xét.",
            cancellationToken);

        var dto = await ReloadAsync<PracticeSubmissionReviewDto>(submission.Id, cancellationToken);
        dto.Feedback = dto.Feedbacks.Single(x => x.Id == feedback.Id);
        return Result<PracticeSubmissionReviewDto>.Success(dto);
    }

    public async Task<Result<PracticeSubmissionDetailDto>> AddFeedbackAsync(
        Guid directorUserId, Guid id, AddPracticeFeedbackRequest request, CancellationToken cancellationToken)
    {
        var submission = await practiceSubmissionRepository.GetForReviewAsync(id, cancellationToken);
        if (submission is null
            || !await choirScope.CanManageChoirAsync(submission.PracticeAssignment.ChoirId, cancellationToken))
        {
            return Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotFound);
        }

        // The first review goes through ReviewAsync, which also enforces the newest-attempt rule.
        if (submission.Status is not (SubmissionStatus.Passed or SubmissionStatus.NeedsRevision))
            return Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotReviewed);

        var isResultChange = request.Result is { } newResult && newResult != submission.Status;

        // Changing an older attempt's result would contradict the newer attempt the member is working on.
        if (isResultChange && !await practiceSubmissionRepository.IsLatestAttemptAsync(submission, cancellationToken))
            return Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionSuperseded);

        if (isResultChange) submission.Status = request.Result!.Value;
        var feedback = NewFeedback(submission, directorUserId, request.Comment);

        // Only a result change touches the status token; a plain comment is an insert and never conflicts.
        if (!await practiceSubmissionRepository.TrySaveReviewAsync(submission, feedback, cancellationToken))
            return Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionAlreadyReviewed);

        var title = submission.PracticeAssignment.Title;
        var resultText = submission.Status == SubmissionStatus.Passed ? "đã đạt yêu cầu" : "được đánh dấu cần chỉnh sửa";
        await NotifyMemberAsync(
            submission,
            isResultChange ? "Kết quả bài tập đã thay đổi" : "Nhận xét mới cho bài tập",
            isResultChange
                ? $"Bản thu \"{title}\" của bạn hiện {resultText}."
                : $"Ca trưởng đã để lại nhận xét mới cho bản thu \"{title}\" của bạn.",
            cancellationToken);

        return Result<PracticeSubmissionDetailDto>.Success(
            await ReloadAsync<PracticeSubmissionDetailDto>(submission.Id, cancellationToken));
    }

    public async Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetMineAsync(
        Guid userId, SearchMyPracticeSubmissionsRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PagedList<PracticeSubmissionDetailDto>>.Failure(ErrorCodes.MemberNotFound);

        var page = await practiceSubmissionRepository.SearchForMemberAsync(member.Id, request, cancellationToken);
        return Result<PagedList<PracticeSubmissionDetailDto>>.Success(ToDtoPage(page));
    }

    public async Task<Result<PracticeSubmissionDetailDto>> GetMineByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.MemberNotFound);

        // Another member's submission is reported as missing, not forbidden.
        var submission = await practiceSubmissionRepository.GetWithMemberAsync(id, cancellationToken);
        return submission is null || submission.MemberId != member.Id
            ? Result<PracticeSubmissionDetailDto>.Failure(ErrorCodes.PracticeSubmissionNotFound)
            : Result<PracticeSubmissionDetailDto>.Success(ToDto<PracticeSubmissionDetailDto>(submission));
    }

    /// <summary>A review entry carrying the submission's result after this change.</summary>
    private static PracticeFeedback NewFeedback(PracticeSubmission submission, Guid reviewerId, string? comment) => new()
    {
        Id = Guid.NewGuid(),
        SubmissionId = submission.Id,
        ReviewerId = reviewerId,
        Result = submission.Status,
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
        ReviewedAt = DateTime.UtcNow,
    };

    // The comment stays out of the content: it can fill the whole 1000-character column on its own.
    private Task NotifyMemberAsync(
        PracticeSubmission submission, string title, string content, CancellationToken cancellationToken) =>
        notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.PracticeFeedback, title, content, [submission.Member.UserId],
                nameof(PracticeSubmission), submission.Id),
            cancellationToken);

    /// <summary>Reads the saved submission back, so every feedback comes with its reviewer's name.</summary>
    private async Task<TDto> ReloadAsync<TDto>(Guid id, CancellationToken cancellationToken)
        where TDto : PracticeSubmissionDetailDto =>
        ToDto<TDto>((await practiceSubmissionRepository.GetWithMemberAsync(id, cancellationToken))!);

    private TDto ToDto<TDto>(PracticeSubmission submission) where TDto : PracticeSubmissionDetailDto
    {
        var dto = mapper.Map<TDto>(submission);
        dto.AudioUrl = fileStorageService.GetSignedUrl(submission.AudioPublicId);
        return dto;
    }

    private PagedList<PracticeSubmissionDetailDto> ToDtoPage(PagedList<PracticeSubmission> page) =>
        new(page.Items.Select(ToDto<PracticeSubmissionDetailDto>).ToList(), page.PageNumber, page.PageSize, page.TotalCount);
}

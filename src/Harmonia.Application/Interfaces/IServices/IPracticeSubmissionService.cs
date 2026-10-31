using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>The Choir Director's side of practice submissions (UC-29).</summary>
public interface IPracticeSubmissionService
{
    /// <summary>Submissions of one assignment, for listening member by member (FE-42).</summary>
    Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetByAssignmentAsync(
        Guid assignmentId, SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken);

    /// <summary>Submissions across every assignment, oldest first: the review queue (FE-42).</summary>
    Task<Result<PagedList<PracticeSubmissionDetailDto>>> SearchAsync(
        SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken);

    /// <summary>One submission with a freshly signed audio URL, fetched right before playback.</summary>
    Task<Result<PracticeSubmissionDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Grades the member's newest attempt as Passed or NeedsRevision with an optional comment (FE-43, FE-44),
    /// then notifies the member (S-05). A submission is reviewed once.
    /// </summary>
    Task<Result<PracticeSubmissionReviewDto>> ReviewAsync(
        Guid directorUserId, Guid id, ReviewPracticeSubmissionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a comment to a reviewed submission, optionally changing its result (FE-44). A result change is
    /// allowed only on the member's newest attempt. Each call is a new feedback entry; the member is notified.
    /// </summary>
    Task<Result<PracticeSubmissionDetailDto>> AddFeedbackAsync(
        Guid directorUserId, Guid id, AddPracticeFeedbackRequest request, CancellationToken cancellationToken);

    /// <summary>Every attempt of the calling member with its feedback, newest first (UC-10 / FE-12, FE-13).</summary>
    Task<Result<PagedList<PracticeSubmissionDetailDto>>> GetMineAsync(
        Guid userId, SearchMyPracticeSubmissionsRequest request, CancellationToken cancellationToken);

    /// <summary>One submission of the calling member; another member's is reported as missing, not forbidden.</summary>
    Task<Result<PracticeSubmissionDetailDto>> GetMineByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken);
}

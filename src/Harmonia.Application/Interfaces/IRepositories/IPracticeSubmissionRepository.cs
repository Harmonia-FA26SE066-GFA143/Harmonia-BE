using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IPracticeSubmissionRepository : IGenericRepository<PracticeSubmission>
{
    /// <summary>
    /// Inserts the submission and saves. Returns false when a concurrent request already inserted the same
    /// attempt number for this assignment and member, so the unique index rejected this one.
    /// </summary>
    Task<bool> TryAddAsync(PracticeSubmission submission, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of submissions with <see cref="PracticeSubmission.Member"/> and its User, and
    /// <see cref="PracticeSubmission.PracticeAssignment"/> loaded, plus <see cref="PracticeSubmission.Feedbacks"/>
    /// with each Reviewer. Only each member's newest attempt per assignment
    /// unless <see cref="SearchPracticeSubmissionsRequest.AllAttempts"/> is set; the status filter applies after that.
    /// With <paramref name="assignmentId"/>: that assignment only, ordered by member name then newest attempt first.
    /// Without it: every assignment, oldest submission first (the review queue).
    /// </summary>
    Task<PagedList<PracticeSubmission>> SearchAsync(
        Guid? assignmentId, SearchPracticeSubmissionsRequest filter, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of every attempt of one member, newest submission first, optionally for one assignment.
    /// Loaded like <see cref="SearchAsync"/>.
    /// </summary>
    Task<PagedList<PracticeSubmission>> SearchForMemberAsync(
        Guid memberId, SearchMyPracticeSubmissionsRequest filter, CancellationToken cancellationToken);

    /// <summary>Read-only, with Member and its User, the assignment, and Feedbacks with each Reviewer loaded.</summary>
    Task<PracticeSubmission?> GetWithMemberAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked, with Member and its User, the assignment, and Feedbacks with each Reviewer loaded.</summary>
    Task<PracticeSubmission?> GetForReviewAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when the member has no later attempt of the same assignment.</summary>
    Task<bool> IsLatestAttemptAsync(PracticeSubmission submission, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="feedback"/> and saves the status change of a row loaded by
    /// <see cref="GetForReviewAsync"/>. Returns false when another request changed the row's status after it
    /// was read, so nothing is saved.
    /// </summary>
    Task<bool> TrySaveReviewAsync(
        PracticeSubmission submission, PracticeFeedback feedback, CancellationToken cancellationToken);
}

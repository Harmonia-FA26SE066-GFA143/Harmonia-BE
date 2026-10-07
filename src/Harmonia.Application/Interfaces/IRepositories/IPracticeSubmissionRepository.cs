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
    /// <see cref="PracticeSubmission.PracticeAssignment"/> loaded. Only each member's newest attempt per assignment
    /// unless <see cref="SearchPracticeSubmissionsRequest.AllAttempts"/> is set; the status filter applies after that.
    /// With <paramref name="assignmentId"/>: that assignment only, ordered by member name then newest attempt first.
    /// Without it: every assignment, oldest submission first (the review queue).
    /// </summary>
    Task<PagedList<PracticeSubmission>> SearchAsync(
        Guid? assignmentId, SearchPracticeSubmissionsRequest filter, CancellationToken cancellationToken);

    /// <summary>Read-only, with <see cref="PracticeSubmission.Member"/> and its User, and the assignment loaded.</summary>
    Task<PracticeSubmission?> GetWithMemberAsync(Guid id, CancellationToken cancellationToken);
}

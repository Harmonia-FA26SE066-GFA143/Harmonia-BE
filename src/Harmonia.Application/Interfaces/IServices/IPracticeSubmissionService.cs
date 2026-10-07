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
}

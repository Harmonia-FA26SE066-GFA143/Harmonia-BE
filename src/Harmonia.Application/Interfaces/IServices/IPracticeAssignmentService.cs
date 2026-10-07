using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IPracticeAssignmentService
{
    /// <summary>
    /// Creates a practice assignment for every active member, for members with an approved skill, or for
    /// chosen active members (UC-28 / FE-41), then notifies the members it reaches (S-05).
    /// </summary>
    Task<Result<PracticeAssignmentDto>> CreateAsync(
        CreatePracticeAssignmentRequest request, CancellationToken cancellationToken);

    /// <summary>Assignments the calling member currently receives (UC-09 / FE-10), with their own newest submission.</summary>
    Task<Result<PagedList<PracticeAssignmentDetailDto>>> GetMineAsync(
        Guid userId, SearchMyPracticeAssignmentsRequest request, CancellationToken cancellationToken);

    /// <summary>One assignment of the calling member; one they do not receive is reported as missing, not forbidden.</summary>
    Task<Result<PracticeAssignmentDetailDto>> GetMineByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the calling member's recorded audio as their next attempt (UC-09 / FE-11). Allowed until the due
    /// date while the newest attempt is not Passed.
    /// </summary>
    Task<Result<PracticeSubmissionDto>> SubmitAsync(
        Guid userId, Guid assignmentId, CreatePracticeSubmissionRequest request, FileContent? file,
        CancellationToken cancellationToken);
}

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
}

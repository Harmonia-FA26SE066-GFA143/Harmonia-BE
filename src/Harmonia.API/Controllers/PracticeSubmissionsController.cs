using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Practice submissions: the Choir Director listens, grades and comments (UC-29); Choir Members read their own
/// submissions with the feedback (UC-10).
/// </summary>
[Route("api/practice-submissions")]
[Authorize]
public class PracticeSubmissionsController(IPracticeSubmissionService practiceSubmissionService) : ApiControllerBase
{
    /// <summary>Review queue across every assignment, oldest first (FE-42).</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.SearchAsync(request, cancellationToken));

    /// <summary>One submission with a fresh signed audio URL; call it right before playback.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.GetByIdAsync(id, cancellationToken));

    /// <summary>Grades the submission as Passed or NeedsRevision with an optional comment (FE-43, FE-44).</summary>
    [HttpPost("{id:guid}/feedback")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> ReviewAsync(
        Guid id, [FromBody] ReviewPracticeSubmissionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.ReviewAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>Adds a comment to a graded submission, optionally changing its result (FE-44).</summary>
    [HttpPost("{id:guid}/comments")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> AddFeedbackAsync(
        Guid id, [FromBody] AddPracticeFeedbackRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.AddFeedbackAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>The calling member's submissions with status and feedback, newest first (UC-10 / FE-12, FE-13).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] SearchMyPracticeSubmissionsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.GetMineAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>One of the calling member's submissions; opened from a PracticeFeedback notification.</summary>
    [HttpGet("mine/{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.GetMineByIdAsync(User.GetUserId(), id, cancellationToken));
}

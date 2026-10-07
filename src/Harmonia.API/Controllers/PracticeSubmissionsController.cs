using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Practice submissions as the Choir Director reviews them (UC-29).</summary>
[Route("api/practice-submissions")]
[Authorize(Roles = RoleNames.ChoirDirector)]
public class PracticeSubmissionsController(IPracticeSubmissionService practiceSubmissionService) : ApiControllerBase
{
    /// <summary>Review queue across every assignment, oldest first (FE-42).</summary>
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchPracticeSubmissionsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.SearchAsync(request, cancellationToken));

    /// <summary>One submission with a fresh signed audio URL; call it right before playback.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.GetByIdAsync(id, cancellationToken));

    /// <summary>Grades the submission as Passed or NeedsRevision with an optional comment (FE-43, FE-44).</summary>
    [HttpPost("{id:guid}/feedback")]
    public async Task<IActionResult> ReviewAsync(
        Guid id, [FromBody] ReviewPracticeSubmissionRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceSubmissionService.ReviewAsync(User.GetUserId(), id, request, cancellationToken));
}

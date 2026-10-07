using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/practice-assignments")]
[Authorize]
public class PracticeAssignmentsController(IPracticeAssignmentService practiceAssignmentService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePracticeAssignmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceAssignmentService.CreateAsync(request, cancellationToken));

    /// <summary>Assignments the calling member receives (UC-09 / FE-10).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] SearchMyPracticeAssignmentsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceAssignmentService.GetMineAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("mine/{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await practiceAssignmentService.GetMineByIdAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>Submits the calling member's recorded audio as their next attempt (UC-09 / FE-11).</summary>
    [HttpPost("{id:guid}/submissions")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitAsync(
        Guid id, [FromForm] CreatePracticeSubmissionRequest request, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return ToActionResult(await practiceAssignmentService.SubmitAsync(User.GetUserId(), id, request, null, cancellationToken));

        await using var content = file.OpenReadStream();
        return ToActionResult(await practiceAssignmentService.SubmitAsync(
            User.GetUserId(), id, request, new FileContent(content, file.FileName, file.Length), cancellationToken));
    }
}

using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/practice-assignments")]
[Authorize(Roles = RoleNames.ChoirDirector)]
public class PracticeAssignmentsController(IPracticeAssignmentService practiceAssignmentService) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePracticeAssignmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await practiceAssignmentService.CreateAsync(request, cancellationToken));
}

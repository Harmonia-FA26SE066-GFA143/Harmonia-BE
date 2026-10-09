using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Rehearsals and preparation sessions; the Choir Director takes attendance (UC-30 / FE-45).</summary>
[Route("api/rehearsals")]
[Authorize(Roles = RoleNames.ChoirDirector)]
public class RehearsalsController(IRehearsalAttendanceService rehearsalAttendanceService) : ApiControllerBase
{
    [HttpGet("{id:guid}/attendances")]
    public async Task<IActionResult> GetAttendancesAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalAttendanceService.GetAttendancesAsync(id, cancellationToken));

    [HttpPut("{id:guid}/attendances")]
    public async Task<IActionResult> RecordAttendancesAsync(
        Guid id, [FromBody] RecordRehearsalAttendancesRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalAttendanceService.RecordAsync(User.GetUserId(), id, request, cancellationToken));
}

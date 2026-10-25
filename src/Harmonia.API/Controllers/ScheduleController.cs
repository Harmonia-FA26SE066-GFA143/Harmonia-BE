using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/schedule")]
[Authorize]
public class ScheduleController(IUpcomingScheduleService upcomingScheduleService) : ApiControllerBase
{
    [HttpGet("events")]
    [ProducesResponseType<List<LiturgicalEventSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingEventsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetUpcomingEventsAsync(cancellationToken));

    [HttpGet("events/{id:guid}")]
    [ProducesResponseType<MemberEventDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventDetailAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetEventDetailAsync(User.GetUserId(), id, cancellationToken));

    [HttpGet("rehearsals")]
    [ProducesResponseType<List<RehearsalSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingRehearsalsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetUpcomingRehearsalsAsync(User.GetUserId(), cancellationToken));

    [HttpGet("rehearsals/{id:guid}")]
    [ProducesResponseType<MemberRehearsalDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRehearsalDetailAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetRehearsalDetailAsync(User.GetUserId(), id, cancellationToken));

    [HttpGet("assignments")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<List<MyAssignedEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAssignmentsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await upcomingScheduleService.GetMyAssignmentsAsync(User.GetUserId(), cancellationToken));
}
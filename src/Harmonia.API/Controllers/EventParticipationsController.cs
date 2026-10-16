using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/event-participations")]
[Authorize]
public class EventParticipationsController(IEventParticipationService eventParticipationService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<List<EventParticipationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendRequestsAsync(
        [FromBody] SendParticipationRequestsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await eventParticipationService.SendRequestsAsync(request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<EventParticipationSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEventAsync(
        [FromQuery] Guid eventId, CancellationToken cancellationToken) =>
        ToActionResult(await eventParticipationService.GetByEventAsync(eventId, cancellationToken));

    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<List<MyParticipationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsync(CancellationToken cancellationToken) =>
        ToActionResult(await eventParticipationService.GetMineAsync(User.GetUserId(), cancellationToken));

    [HttpPut("mine/{eventId:guid}")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<EventParticipationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondAsync(
        Guid eventId, [FromBody] RespondParticipationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await eventParticipationService.RespondAsync(User.GetUserId(), eventId, request, cancellationToken));
}
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Service roster of an event (UC-25 / FE-36): only the Choir Director builds it.</summary>
[Route("api/service-rosters")]
[Authorize(Roles = RoleNames.ChoirDirector)]
public class ServiceRostersController(IRosterService rosterService) : ApiControllerBase
{
    [HttpPost("suggestions")]
    [ProducesResponseType<RosterSuggestionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SuggestAsync(
        [FromBody] SuggestServiceRosterRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.SuggestAsync(request, cancellationToken));

    /// <summary>Current roster of the event, to reopen a saved roster (UC-25b / FE-38).</summary>
    [HttpGet]
    [ProducesResponseType<ServiceRosterDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEventAsync(
        [FromQuery] Guid eventId, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.GetByEventAsync(eventId, cancellationToken));

    /// <summary>Shortage warnings of the event's current roster (UC-25a / FE-37).</summary>
    [HttpGet("shortages")]
    [ProducesResponseType<List<RosterShortageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShortagesAsync(
        [FromQuery] Guid eventId, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.GetShortagesAsync(eventId, cancellationToken));

    /// <summary>Manually assigns a member to a song / skill (UC-25b / FE-38).</summary>
    [HttpPost("assignments")]
    [ProducesResponseType<RosterAssignmentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAssignmentAsync(
        [FromBody] CreateRosterAssignmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.AddAssignmentAsync(request, cancellationToken));

    /// <summary>Replaces the member of an assignment; the old line is kept as Replaced (UC-25b / FE-38).</summary>
    [HttpPost("assignments/{id:guid}/replacement")]
    [ProducesResponseType<RosterAssignmentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceAssignmentAsync(
        Guid id, [FromBody] ReplaceRosterAssignmentRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.ReplaceAssignmentAsync(id, request, cancellationToken));

    /// <summary>Removes a member from the roster (UC-25b / FE-38).</summary>
    [HttpDelete("assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveAssignmentAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.RemoveAssignmentAsync(id, cancellationToken));

    /// <summary>Locks the roster after the song list is approved; shortages do not block (UC-26 / FE-39).</summary>
    [HttpPost("{id:guid}/finalization")]
    [ProducesResponseType<ServiceRosterDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> FinalizeAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.FinalizeAsync(id, cancellationToken));

    /// <summary>Notifies the selected members, or every member not notified yet, of their assignment (UC-27 / FE-40).</summary>
    [HttpPost("{id:guid}/notifications")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendNotificationsAsync(
        Guid id, [FromBody] SendRosterNotificationsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.SendNotificationsAsync(id, request, cancellationToken));
}

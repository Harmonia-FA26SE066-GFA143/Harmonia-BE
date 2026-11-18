using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Members asking to join or transfer to a choir, and directors' invitations (2026-11-18). A Choir Member asks; the
/// choir's director or the Admin answers. Invitations are created through POST api/choirs/{id}/members and the
/// member answers. Who may act on a row is decided by the service; anything else reads as not found.
/// </summary>
[Route("api/choir-join-requests")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector},{RoleNames.ChoirMember}")]
public class ChoirJoinRequestsController(IChoirJoinRequestService choirJoinRequestService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<ChoirJoinRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateChoirJoinRequestRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirJoinRequestService.CreateAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedList<ChoirJoinRequestDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchChoirJoinRequestsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirJoinRequestService.SearchAsync(request, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ChoirJoinRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await choirJoinRequestService.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ChoirJoinRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(
        Guid id, [FromBody] RejectChoirJoinRequestRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirJoinRequestService.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ChoirJoinRequestDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await choirJoinRequestService.CancelAsync(id, cancellationToken));
}

using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/song-lists")]
[Authorize]
public class SongListsController(ISongListService songListService) : ApiControllerBase
{
    [HttpGet("event/{eventId:guid}/approved")]
    public async Task<IActionResult> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        ToActionResult(await songListService.GetApprovedForEventAsync(eventId, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RoleNames.ChoirDirector},{RoleNames.ParishPriest}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await songListService.GetByIdAsync(id, cancellationToken));

    [HttpGet("pending")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    public async Task<IActionResult> GetPendingAsync(CancellationToken cancellationToken) =>
        ToActionResult(await songListService.GetPendingAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateSongListRequest request, CancellationToken cancellationToken)
    {
        var proposedBy = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return ToActionResult(await songListService.CreateAsync(request, proposedBy, cancellationToken));
    }

    [HttpPut("{id:guid}/items")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateItemsAsync(
        Guid id, [FromBody] UpdateSongListItemsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songListService.UpdateItemsAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> SubmitAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await songListService.SubmitAsync(id, cancellationToken));

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    public async Task<IActionResult> ReviewAsync(
        Guid id, [FromBody] ReviewSongListRequest request, CancellationToken cancellationToken)
    {
        var reviewerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return ToActionResult(await songListService.ReviewAsync(id, request, reviewerId, cancellationToken));
    }
}
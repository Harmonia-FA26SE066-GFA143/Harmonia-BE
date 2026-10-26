using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Choirs of the parish and their members (D7). The Admin creates choirs and assigns their director;
/// each Choir Director sees and manages the members of their own choirs. The Parish Priest reads the list to assign
/// choirs to events.
/// </summary>
[Route("api/choirs")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector},{RoleNames.ParishPriest}")]
public class ChoirsController(IChoirService choirService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ChoirDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateChoirRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.CreateAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType<List<ChoirDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken) =>
        ToActionResult(await choirService.GetAllAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<ChoirDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateChoirRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.UpdateAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/members")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<List<ChoirMemberDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembersAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.GetMembersAsync(id, cancellationToken));

    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMembersAsync(
        Guid id, [FromBody] AddChoirMembersRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.AddMembersAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.RemoveMemberAsync(id, memberId, cancellationToken));
}

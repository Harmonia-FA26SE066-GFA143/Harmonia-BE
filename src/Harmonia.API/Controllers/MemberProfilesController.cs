using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Choir member records: a member reads and edits their own, the Choir Director manages everyone's.</summary>
[Route("api/member-profiles")]
[Authorize]
public class MemberProfilesController(IMemberProfileService memberProfileService) : ApiControllerBase
{
    [HttpGet("me")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.GetMineAsync(User.GetUserId(), cancellationToken));

    [HttpPut("me")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> UpdateMineAsync(
        [FromBody] UpdateMyMemberProfileRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.UpdateMineAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchMemberProfilesRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.GetByIdAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateMemberProfileRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberProfileService.UpdateAsync(id, request, cancellationToken));
}

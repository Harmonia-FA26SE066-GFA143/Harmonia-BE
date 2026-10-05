using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Skills declared by choir members: a member declares and tracks their own (UC-03),
/// the Choir Director approves or rejects them (UC-19).
/// </summary>
[Route("api/member-skills")]
[Authorize]
public class MemberSkillsController(IMemberSkillService memberSkillService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> DeclareAsync(
        [FromBody] DeclareMemberSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.DeclareAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] ApprovalStatus? status, [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetMineAsync(User.GetUserId(), status, paging, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetMineByIdAsync(User.GetUserId(), id, cancellationToken));

    [HttpGet("pending")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetPendingAsync(
        [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetPendingAsync(paging, cancellationToken));

    [HttpPatch("{id:guid}/approve")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.ApproveAsync(User.GetUserId(), id, cancellationToken));

    [HttpPatch("{id:guid}/reject")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> RejectAsync(
        Guid id, [FromBody] RejectMemberSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.RejectAsync(User.GetUserId(), id, request, cancellationToken));
}

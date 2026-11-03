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
    // A 100 MB clip plus multipart overhead, so the service still answers with its own error code.
    // Keep in step with maxAllowedContentLength in web.config.
    private const long MaxEvidenceRequestBytes = 101 * 1024 * 1024;

    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<MemberSkillDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeclareAsync(
        [FromBody] DeclareMemberSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.DeclareAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<PagedList<MemberSkillDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] ApprovalStatus? status, [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetMineAsync(User.GetUserId(), status, paging, cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [ProducesResponseType<MemberSkillDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetMineByIdAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>The member attaches a voice (.mp3, .m4a, .wav, max 20 MB) or clip (.mp4, .mov, max 100 MB) to their Pending declaration.</summary>
    [HttpPut("{id:guid}/evidence")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    [RequestSizeLimit(MaxEvidenceRequestBytes)]
    [ProducesResponseType<MemberSkillDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadEvidenceAsync(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return ToActionResult(await memberSkillService.UploadEvidenceAsync(User.GetUserId(), id, null, cancellationToken));

        await using var content = file.OpenReadStream();
        return ToActionResult(await memberSkillService.UploadEvidenceAsync(
            User.GetUserId(), id, new FileContent(content, file.FileName, file.Length), cancellationToken));
    }

    [HttpGet("pending")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<PagedList<MemberSkillDetailDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingAsync(
        [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.GetPendingAsync(User.GetUserId(), paging, cancellationToken));

    [HttpPatch("{id:guid}/approve")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<MemberSkillDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.ApproveAsync(User.GetUserId(), id, cancellationToken));

    [HttpPatch("{id:guid}/reject")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<MemberSkillDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(
        Guid id, [FromBody] RejectMemberSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberSkillService.RejectAsync(User.GetUserId(), id, request, cancellationToken));
}

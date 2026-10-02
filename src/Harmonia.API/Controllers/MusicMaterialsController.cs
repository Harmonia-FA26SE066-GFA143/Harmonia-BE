using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Harmonia.API.Controllers;

/// <summary>Music materials (UC-21 / FE-28): every role can list, only the Choir Director uploads, edits and deletes.</summary>
[Route("api/music-materials")]
[Authorize]
public class MusicMaterialsController(IMusicMaterialService musicMaterialService) : ApiControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        [FromForm] UploadMusicMaterialRequest request, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return ToActionResult(await musicMaterialService.UploadAsync(request, null, cancellationToken));

        await using var content = file.OpenReadStream();
        return ToActionResult(await musicMaterialService.UploadAsync(
            request, new FileContent(content, file.FileName, file.Length), cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> GetBySongAsync(
        [FromQuery, BindRequired] Guid songId, [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.GetBySongAsync(songId, paging, cancellationToken));

    /// <summary>Materials for the calling member's approved skills plus those for everyone (UC-07 / FE-08).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] Guid? songId, [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.GetMineAsync(User.GetUserId(), songId, paging, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateMusicMaterialRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.DeleteAsync(id, cancellationToken));
}

using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Harmonia.API.Controllers;

/// <summary>
/// Music materials (UC-21 / FE-28): every role can list, only the Choir Director uploads, edits and deletes;
/// Choir Members list their own materials and mark their learning progress.
/// </summary>
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

    /// <summary>
    /// Materials for the calling member's approved skills plus those for everyone (UC-07 / FE-08),
    /// optionally searched and filtered (UC-07E).
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] SearchMusicMaterialsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.GetMineAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Learning status of every active member expected to learn the material (UC-08 / FE-09).</summary>
    [HttpGet("{id:guid}/learning-progress")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetLearningProgressAsync(
        Guid id, [FromQuery] SearchMaterialLearningProgressRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.GetLearningProgressAsync(id, request, cancellationToken));

    /// <summary>Marks the material as learned or as needing practice for the calling member (UC-08 / FE-09).</summary>
    [HttpPut("{id:guid}/learning-progress")]
    [Authorize(Roles = RoleNames.ChoirMember)]
    public async Task<IActionResult> UpdateLearningProgressAsync(
        Guid id, [FromBody] UpdateMaterialLearningProgressRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await musicMaterialService.UpdateLearningProgressAsync(
            User.GetUserId(), id, request, cancellationToken));

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

using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Song library and its classification (UC-21 / FE-27, FE-29): every role can browse, only the Choir Director edits.</summary>
[Route("api/songs")]
[Authorize]
public class SongsController(ISongService songService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchSongsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await songService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateSongRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateSongRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await songService.DeleteAsync(id, cancellationToken));

    [HttpGet("{id:guid}/classification")]
    public async Task<IActionResult> GetClassificationAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await songService.GetClassificationAsync(id, cancellationToken));

    [HttpPut("{id:guid}/classification")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateClassificationAsync(
        Guid id, [FromBody] UpdateSongClassificationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songService.UpdateClassificationAsync(id, request, cancellationToken));
}

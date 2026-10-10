using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Personnel needed per song of an event (UC-24 / FE-35): every role can read, only the Choir Director edits.</summary>
[Route("api/song-list-items")]
[Authorize]
public class SongListItemsController(ISongPersonnelRequirementService personnelRequirementService) : ApiControllerBase
{
    [HttpGet("{id:guid}/personnel-requirements")]
    public async Task<IActionResult> GetPersonnelRequirementsAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await personnelRequirementService.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/personnel-requirements")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdatePersonnelRequirementsAsync(
        Guid id, [FromBody] UpdateSongPersonnelRequirementsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await personnelRequirementService.UpdateAsync(id, request, cancellationToken));
}

using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Service roster of an event (UC-25 / FE-36): only the Choir Director builds it.</summary>
[Route("api/service-rosters")]
[Authorize(Roles = RoleNames.ChoirDirector)]
public class ServiceRostersController(IRosterService rosterService) : ApiControllerBase
{
    [HttpPost("suggestions")]
    public async Task<IActionResult> SuggestAsync(
        [FromBody] SuggestServiceRosterRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rosterService.SuggestAsync(request, cancellationToken));
}

using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.API.Controllers;

[Route("api/liturgical-events")]
[Authorize(Roles = RoleNames.ParishPriest)]
public class LiturgicalEventsController(ILiturgicalEventService liturgicalEventService) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateLiturgicalEventRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.CreateAsync(request, cancellationToken));

    [HttpPatch("{id:guid}/publish")]
    public async Task<IActionResult> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.PublishAsync(id, cancellationToken));
}
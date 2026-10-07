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
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchLiturgicalEventsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateLiturgicalEventRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateLiturgicalEventRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/publish")]
    public async Task<IActionResult> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.PublishAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.CancelAsync(id, cancellationToken));
}

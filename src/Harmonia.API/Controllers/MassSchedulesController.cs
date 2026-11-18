using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Fixed weekly Mass times of the parish. The Admin keeps them; the priest reads them and generates events from them
/// through POST api/liturgical-events/generation.
/// </summary>
[Route("api/mass-schedules")]
[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ParishPriest}")]
public class MassSchedulesController(IMassScheduleService massScheduleService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<MassScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken) =>
        ToActionResult(await massScheduleService.GetAllAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<MassScheduleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateMassScheduleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await massScheduleService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<MassScheduleDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateMassScheduleRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await massScheduleService.UpdateAsync(id, request, cancellationToken));
}

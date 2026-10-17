using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// General system settings (UC-33 / FE-51). Only the Admin lists and edits them; every signed-in role reads the
/// few that are public, such as the parish name.
/// </summary>
[Route("api/system-settings")]
[Authorize]
public class SystemSettingsController(ISystemSettingService systemSettingService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<List<SystemSettingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken) =>
        ToActionResult(await systemSettingService.GetAllAsync(cancellationToken));

    [HttpGet("public")]
    [ProducesResponseType<List<SystemSettingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicAsync(CancellationToken cancellationToken) =>
        ToActionResult(await systemSettingService.GetPublicAsync(cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SystemSettingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateSystemSettingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await systemSettingService.UpdateAsync(id, request, cancellationToken));
}

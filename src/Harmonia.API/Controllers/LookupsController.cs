using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Read-only catalogs for dropdowns, open to every signed-in role. Only active rows are returned.</summary>
[Route("api/lookups")]
[Authorize]
public class LookupsController(ILookupService lookupService) : ApiControllerBase
{
    [HttpGet("mass-types")]
    public async Task<IActionResult> GetMassTypesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetMassTypesAsync(cancellationToken));

    [HttpGet("ceremony-types")]
    public async Task<IActionResult> GetCeremonyTypesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetCeremonyTypesAsync(cancellationToken));

    [HttpGet("event-categories")]
    public async Task<IActionResult> GetEventCategoriesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetEventCategoriesAsync(cancellationToken));

    [HttpGet("song-themes")]
    public async Task<IActionResult> GetSongThemesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSongThemesAsync(cancellationToken));

    [HttpGet("skill-categories")]
    public async Task<IActionResult> GetSkillCategoriesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillCategoriesAsync(cancellationToken));

    [HttpGet("liturgical-seasons")]
    public async Task<IActionResult> GetLiturgicalSeasonsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSeasonsAsync(cancellationToken));

    [HttpGet("liturgical-slots")]
    public async Task<IActionResult> GetLiturgicalSlotsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSlotsAsync(cancellationToken));

    [HttpGet("worship-locations")]
    public async Task<IActionResult> GetWorshipLocationsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetWorshipLocationsAsync(cancellationToken));

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkillsAsync([FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillsAsync(categoryId, cancellationToken));
}

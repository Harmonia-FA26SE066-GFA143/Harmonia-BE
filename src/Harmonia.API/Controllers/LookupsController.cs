using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Catalogs for dropdowns, readable by every signed-in role (active rows only). The Admin also writes them
/// and may read switched-off rows with <c>includeInactive=true</c> (UC-32 / FE-49, FE-50).
/// </summary>
[Route("api/lookups")]
[Authorize]
public class LookupsController(ILookupService lookupService) : ApiControllerBase
{
    /// <summary>Only the Admin sees switched-off rows; for other roles the flag is ignored.</summary>
    private bool ShowInactive(bool includeInactive) => includeInactive && User.IsInRole(RoleNames.Admin);

    [HttpGet("mass-types")]
    public async Task<IActionResult> GetMassTypesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetMassTypesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("mass-types")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateMassTypeAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveMassTypeAsync(null, request, cancellationToken));

    [HttpPut("mass-types/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateMassTypeAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveMassTypeAsync(id, request, cancellationToken));

    [HttpGet("ceremony-types")]
    public async Task<IActionResult> GetCeremonyTypesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetCeremonyTypesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("ceremony-types")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateCeremonyTypeAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveCeremonyTypeAsync(null, request, cancellationToken));

    [HttpPut("ceremony-types/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateCeremonyTypeAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveCeremonyTypeAsync(id, request, cancellationToken));

    [HttpGet("event-categories")]
    public async Task<IActionResult> GetEventCategoriesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetEventCategoriesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("event-categories")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateEventCategoryAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveEventCategoryAsync(null, request, cancellationToken));

    [HttpPut("event-categories/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateEventCategoryAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveEventCategoryAsync(id, request, cancellationToken));

    [HttpGet("song-themes")]
    public async Task<IActionResult> GetSongThemesAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSongThemesAsync(cancellationToken));

    [HttpGet("skill-categories")]
    public async Task<IActionResult> GetSkillCategoriesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillCategoriesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("skill-categories")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateSkillCategoryAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillCategoryAsync(null, request, cancellationToken));

    [HttpPut("skill-categories/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateSkillCategoryAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillCategoryAsync(id, request, cancellationToken));

    [HttpGet("liturgical-seasons")]
    public async Task<IActionResult> GetLiturgicalSeasonsAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSeasonsAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("liturgical-seasons")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateLiturgicalSeasonAsync([FromBody] SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSeasonAsync(null, request, cancellationToken));

    [HttpPut("liturgical-seasons/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateLiturgicalSeasonAsync(Guid id, [FromBody] SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSeasonAsync(id, request, cancellationToken));

    [HttpGet("liturgical-slots")]
    public async Task<IActionResult> GetLiturgicalSlotsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSlotsAsync(cancellationToken));

    [HttpGet("worship-locations")]
    public async Task<IActionResult> GetWorshipLocationsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetWorshipLocationsAsync(cancellationToken));

    [HttpGet("skills")]
    public async Task<IActionResult> GetSkillsAsync(
        [FromQuery] Guid? categoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillsAsync(categoryId, ShowInactive(includeInactive), cancellationToken));

    [HttpPost("skills")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CreateSkillAsync([FromBody] SaveSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillAsync(null, request, cancellationToken));

    [HttpPut("skills/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> UpdateSkillAsync(Guid id, [FromBody] SaveSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillAsync(id, request, cancellationToken));
}

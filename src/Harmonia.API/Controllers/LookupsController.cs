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
    [ProducesResponseType<List<MassTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMassTypesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetMassTypesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("mass-types")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<MassTypeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateMassTypeAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveMassTypeAsync(null, request, cancellationToken));

    [HttpPut("mass-types/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<MassTypeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMassTypeAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveMassTypeAsync(id, request, cancellationToken));

    [HttpGet("ceremony-types")]
    [ProducesResponseType<List<CeremonyTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCeremonyTypesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetCeremonyTypesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("ceremony-types")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<CeremonyTypeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCeremonyTypeAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveCeremonyTypeAsync(null, request, cancellationToken));

    [HttpPut("ceremony-types/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<CeremonyTypeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCeremonyTypeAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveCeremonyTypeAsync(id, request, cancellationToken));

    [HttpGet("event-categories")]
    [ProducesResponseType<List<EventCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEventCategoriesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetEventCategoriesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("event-categories")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<EventCategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEventCategoryAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveEventCategoryAsync(null, request, cancellationToken));

    [HttpPut("event-categories/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<EventCategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateEventCategoryAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveEventCategoryAsync(id, request, cancellationToken));

    [HttpGet("song-themes")]
    [ProducesResponseType<List<SongThemeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSongThemesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSongThemesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("song-themes")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SongThemeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSongThemeAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSongThemeAsync(null, request, cancellationToken));

    [HttpPut("song-themes/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SongThemeDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSongThemeAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSongThemeAsync(id, request, cancellationToken));

    [HttpGet("skill-categories")]
    [ProducesResponseType<List<SkillCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkillCategoriesAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillCategoriesAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("skill-categories")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SkillCategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSkillCategoryAsync([FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillCategoryAsync(null, request, cancellationToken));

    [HttpPut("skill-categories/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SkillCategoryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSkillCategoryAsync(Guid id, [FromBody] SaveCatalogItemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillCategoryAsync(id, request, cancellationToken));

    [HttpGet("liturgical-seasons")]
    [ProducesResponseType<List<LiturgicalSeasonDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLiturgicalSeasonsAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSeasonsAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("liturgical-seasons")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<LiturgicalSeasonDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLiturgicalSeasonAsync([FromBody] SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSeasonAsync(null, request, cancellationToken));

    [HttpPut("liturgical-seasons/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<LiturgicalSeasonDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLiturgicalSeasonAsync(Guid id, [FromBody] SaveLiturgicalSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSeasonAsync(id, request, cancellationToken));

    [HttpGet("liturgical-slots")]
    [ProducesResponseType<List<LiturgicalSlotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLiturgicalSlotsAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetLiturgicalSlotsAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("liturgical-slots")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<LiturgicalSlotDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLiturgicalSlotAsync([FromBody] SaveLiturgicalSlotRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSlotAsync(null, request, cancellationToken));

    [HttpPut("liturgical-slots/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<LiturgicalSlotDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLiturgicalSlotAsync(Guid id, [FromBody] SaveLiturgicalSlotRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveLiturgicalSlotAsync(id, request, cancellationToken));

    [HttpGet("worship-locations")]
    [ProducesResponseType<List<WorshipLocationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorshipLocationsAsync([FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetWorshipLocationsAsync(ShowInactive(includeInactive), cancellationToken));

    [HttpPost("worship-locations")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<WorshipLocationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateWorshipLocationAsync([FromBody] SaveWorshipLocationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveWorshipLocationAsync(null, request, cancellationToken));

    [HttpPut("worship-locations/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<WorshipLocationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWorshipLocationAsync(Guid id, [FromBody] SaveWorshipLocationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveWorshipLocationAsync(id, request, cancellationToken));

    [HttpGet("skills")]
    [ProducesResponseType<List<SkillDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkillsAsync(
        [FromQuery] Guid? categoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.GetSkillsAsync(categoryId, ShowInactive(includeInactive), cancellationToken));

    [HttpPost("skills")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SkillDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSkillAsync([FromBody] SaveSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillAsync(null, request, cancellationToken));

    [HttpPut("skills/{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<SkillDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSkillAsync(Guid id, [FromBody] SaveSkillRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await lookupService.SaveSkillAsync(id, request, cancellationToken));
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Vietnamese names of the English calendar words (celebrations, ranks, seasons). The Admin keeps them; every role
/// reads the result through the *Vi fields of api/liturgical-days.
/// </summary>
[Route("api/liturgical-name-translations")]
[Authorize(Roles = RoleNames.Admin)]
public class LiturgicalNameTranslationsController(ILiturgicalNameTranslationService translationService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedList<LiturgicalNameTranslationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchLiturgicalNameTranslationsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await translationService.SearchAsync(request, cancellationToken));

    /// <summary>Names of the synced days still shown in English.</summary>
    [HttpGet("missing")]
    [ProducesResponseType<List<UntranslatedLiturgicalNameResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMissingAsync(CancellationToken cancellationToken) =>
        ToActionResult(await translationService.GetMissingAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<LiturgicalNameTranslationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await translationService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<LiturgicalNameTranslationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] SaveLiturgicalNameTranslationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await translationService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await translationService.DeleteAsync(id, cancellationToken));
}

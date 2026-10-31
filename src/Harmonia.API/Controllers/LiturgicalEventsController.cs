using Harmonia.Application.Common.Models;
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
[Authorize]
public class LiturgicalEventsController(
    ILiturgicalEventService liturgicalEventService,
    IEventPreparationService eventPreparationService,
    IChoirService choirService,
    IMassScheduleService massScheduleService,
    IEventPersonnelRequirementService eventPersonnelRequirementService,
    ISongSuggestionService songSuggestionService) : ApiControllerBase
{
    /// <summary>Draft events are listed for the priest only (B23).</summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<PagedList<LiturgicalEventListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchLiturgicalEventsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.SearchAsync(
            request, User.IsInRole(RoleNames.ParishPriest), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<LiturgicalEventDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<LiturgicalEventDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateLiturgicalEventRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.CreateAsync(request, cancellationToken));

    /// <summary>Creates Draft events from the parish's fixed Mass times for a date range.</summary>
    [HttpPost("generation")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<List<LiturgicalEventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateAsync(
        [FromBody] GenerateLiturgicalEventsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await massScheduleService.GenerateEventsAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<LiturgicalEventDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateLiturgicalEventRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/publish")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<LiturgicalEventDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.PublishAsync(id, cancellationToken));

    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<LiturgicalEventDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await liturgicalEventService.CancelAsync(id, cancellationToken));

    [HttpGet("{id:guid}/preparation-progress")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<List<EventPreparationProgressDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreparationProgressAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await eventPreparationService.GetProgressAsync(id, cancellationToken));

    [HttpGet("{id:guid}/preparation-status")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<EventPreparationStatusDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreparationStatusAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await eventPreparationService.GetStatusAsync(id, cancellationToken));

    /// <summary>Choirs serving the event and which one leads it (D7, D8).</summary>
    [HttpGet("{id:guid}/choirs")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<List<LiturgicalEventChoirDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChoirsAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.GetEventChoirsAsync(id, cancellationToken));

    /// <summary>Replaces the choirs serving the event and sets the lead choir (D7, D8).</summary>
    [HttpPut("{id:guid}/choirs")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<List<LiturgicalEventChoirDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateChoirsAsync(
        Guid id, [FromBody] UpdateEventChoirsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await choirService.UpdateEventChoirsAsync(id, request, cancellationToken));

    /// <summary>Library songs that fit the event (season, Mass or ceremony type) and optionally one slot, best first.</summary>
    [HttpGet("{id:guid}/song-suggestions")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<PagedList<SongSuggestionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSongSuggestionsAsync(
        Guid id, [FromQuery] SearchSongSuggestionsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await songSuggestionService.GetForEventAsync(id, request, cancellationToken));

    /// <summary>People the event needs for the whole service (organist, guitarist, conductor), not per song.</summary>
    [HttpGet("{id:guid}/personnel-requirements")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<List<EventPersonnelRequirementDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonnelRequirementsAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await eventPersonnelRequirementService.GetAsync(id, cancellationToken));

    /// <summary>Replaces the whole-service requirements; only the lead choir's director.</summary>
    [HttpPut("{id:guid}/personnel-requirements")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    [ProducesResponseType<List<EventPersonnelRequirementDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePersonnelRequirementsAsync(
        Guid id, [FromBody] UpdateEventPersonnelRequirementsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await eventPersonnelRequirementService.UpdateAsync(id, request, cancellationToken));
}

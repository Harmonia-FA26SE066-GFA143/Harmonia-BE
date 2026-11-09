using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Member → Choir Director nominations (UC-46). A Choir Director or Parish Priest nominates, the Parish Priest
/// decides, the Admin appoints. Every role reads, filtered by the service to what the caller may see.
/// </summary>
[Route("api/director-nominations")]
[Authorize]
public class DirectorNominationsController(IDirectorNominationService directorNominationService) : ApiControllerBase
{
    [HttpGet("eligibility")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<DirectorNominationStatsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibilityAsync([FromQuery] Guid memberId, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.GetEligibilityAsync(memberId, cancellationToken));

    [HttpPost]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateDirectorNominationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.CreateAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedList<DirectorNominationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchDirectorNominationsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.SearchAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.GetByIdAsync(id, cancellationToken));

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> WithdrawAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.WithdrawAsync(id, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(
        Guid id, [FromBody] ReviewDirectorNominationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelAsync(
        Guid id, [FromBody] ReviewDirectorNominationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.CancelAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/waive-training")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> WaiveTrainingAsync(
        Guid id, [FromBody] ReviewDirectorNominationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.WaiveTrainingAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/appoint")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<DirectorNominationDetailDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AppointAsync(
        Guid id, [FromBody] AppointDirectorNominationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNominationService.AppointAsync(id, request, cancellationToken));
}

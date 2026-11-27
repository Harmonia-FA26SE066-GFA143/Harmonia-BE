using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Member → Choir Director nominations (UC-46). A Choir Director or Parish Priest nominates, the Parish Priest
/// decides and records the training courses, the Admin appoints. Every role reads, filtered by the service to
/// what the caller may see.
/// </summary>
[Route("api/director-nominations")]
[Authorize]
public class DirectorNominationsController(
    IDirectorNominationService directorNominationService,
    IDirectorTrainingService directorTrainingService) : ApiControllerBase
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

    [HttpGet("{id:guid}/trainings")]
    [ProducesResponseType<List<DirectorTrainingDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrainingsAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorTrainingService.GetByNominationAsync(id, cancellationToken));

    [HttpPost("{id:guid}/trainings")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorTrainingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTrainingAsync(
        Guid id, [FromBody] CreateDirectorTrainingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorTrainingService.CreateAsync(id, request, cancellationToken));

    [HttpPut("{id:guid}/trainings/{trainingId:guid}/result")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorTrainingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordTrainingResultAsync(
        Guid id, Guid trainingId, [FromBody] RecordDirectorTrainingResultRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await directorTrainingService.RecordResultAsync(id, trainingId, request, cancellationToken));

    /// <summary>Certificate file (.pdf, .jpg, .png, max 20 MB), stored private; a new upload replaces the old one.</summary>
    [HttpPut("{id:guid}/trainings/{trainingId:guid}/certificate")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<DirectorTrainingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadTrainingCertificateAsync(
        Guid id, Guid trainingId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return ToActionResult(
                await directorTrainingService.UploadCertificateAsync(id, trainingId, null, cancellationToken));
        }

        await using var content = file.OpenReadStream();
        return ToActionResult(await directorTrainingService.UploadCertificateAsync(
            id, trainingId, new FileContent(content, file.FileName, file.Length), cancellationToken));
    }

    [HttpDelete("{id:guid}/trainings/{trainingId:guid}")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTrainingAsync(Guid id, Guid trainingId, CancellationToken cancellationToken) =>
        ToActionResult(await directorTrainingService.DeleteAsync(id, trainingId, cancellationToken));
}

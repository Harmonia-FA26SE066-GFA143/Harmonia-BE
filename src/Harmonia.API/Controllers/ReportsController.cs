using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Harmonia.API.Controllers;

/// <summary>Service history and song usage over past events, for the Parish Priest and the Admin (UC-16 / FE-22).</summary>
[Route("api/reports")]
[Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.Admin}")]
public class ReportsController(IReportService reportService, IReportExportService reportExportService) : ApiControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    [HttpGet("service-history")]
    [ProducesResponseType<PagedList<ServiceHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceHistoryAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetServiceHistoryAsync(request, cancellationToken));

    [HttpGet("song-usage")]
    [ProducesResponseType<PagedList<SongUsageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSongUsageAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetSongUsageAsync(request, cancellationToken));

    // The four reports below are the Admin's (UC-34 / FE-52); the controller attribute still applies, so this narrows it.
    [HttpGet("user-activity")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<UserActivityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserActivityAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetUserActivityAsync(request, cancellationToken));

    [HttpGet("rehearsal-attendance")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<RehearsalAttendanceReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRehearsalAttendanceAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetRehearsalAttendanceAsync(request, cancellationToken));

    [HttpGet("participation")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<ParticipationReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipationAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetParticipationAsync(request, cancellationToken));

    [HttpGet("assignment-completion")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<PracticeAssignmentCompletionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignmentCompletionAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetAssignmentCompletionAsync(request, cancellationToken));

    /// <summary>Downloads any of the six reports as a file and records the export (UC-34E / FE-53).</summary>
    [HttpGet("export")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] ExportReportRequest request, CancellationToken cancellationToken)
    {
        var result = await reportExportService.ExportAsync(User.GetUserId(), request, cancellationToken);
        if (!result.IsSuccess) return ToActionResult(result);

        var file = result.Value!;
        return File(
            file.Content,
            ContentTypes.TryGetContentType(file.FileName, out var contentType) ? contentType : "application/octet-stream",
            file.FileName);
    }

    [HttpGet("exports")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<ReportExportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExportsAsync(
        [FromQuery] PagingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportExportService.GetExportsAsync(request, cancellationToken));
}

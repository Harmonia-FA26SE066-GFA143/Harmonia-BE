using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Harmonia.API.Controllers;

/// <summary>
/// Service history and song usage over past events, for the Parish Priest and the Admin (UC-16 / FE-22);
/// the per-member reports and their export also for the Choir Director.
/// </summary>
[Route("api/reports")]
[Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.Admin},{RoleNames.ChoirDirector}")]
public class ReportsController(IReportService reportService, IReportExportService reportExportService) : ApiControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    [HttpGet("service-history")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.Admin}")]
    [ProducesResponseType<PagedList<ServiceHistoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceHistoryAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetServiceHistoryAsync(request, cancellationToken));

    [HttpGet("song-usage")]
    [Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.Admin}")]
    [ProducesResponseType<PagedList<SongUsageDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSongUsageAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetSongUsageAsync(request, cancellationToken));

    // The reports below are the Admin's (UC-34 / FE-52); the three per-member ones are also the Choir Director's.
    [HttpGet("user-activity")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<UserActivityDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserActivityAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetUserActivityAsync(request, cancellationToken));

    [HttpGet("rehearsal-attendance")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<PagedList<RehearsalAttendanceReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRehearsalAttendanceAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetRehearsalAttendanceAsync(request, cancellationToken));

    [HttpGet("participation")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<PagedList<ParticipationReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipationAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetParticipationAsync(request, cancellationToken));

    [HttpGet("assignment-completion")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<PagedList<PracticeAssignmentCompletionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignmentCompletionAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetAssignmentCompletionAsync(request, cancellationToken));

    /// <summary>
    /// Downloads a report as a file and records the export (UC-34E / FE-53). The Admin exports any of the six,
    /// the Choir Director only the three per-member ones.
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
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

    /// <summary>Attendance and practice totals per week, month or year of a date range, for the same members as the per-member reports.</summary>
    [HttpGet("activity-trend")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.ChoirDirector}")]
    [ProducesResponseType<List<ActivityTrendDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivityTrendAsync(
        [FromQuery] SearchActivityTrendRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetActivityTrendAsync(request, cancellationToken));

    /// <summary>Column keys and Vietnamese titles of a report, the same ones its export uses.</summary>
    [HttpGet("columns/{type}")]
    [ProducesResponseType<List<ReportColumnDto>>(StatusCodes.Status200OK)]
    public IActionResult GetColumns(ReportType type) => ToActionResult(reportExportService.GetColumns(type));

    [HttpGet("exports")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType<PagedList<ReportExportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExportsAsync(
        [FromQuery] PagingRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportExportService.GetExportsAsync(request, cancellationToken));
}

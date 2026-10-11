using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>Service history and song usage over past events, for the Parish Priest and the Admin (UC-16 / FE-22).</summary>
[Route("api/reports")]
[Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.Admin}")]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    [HttpGet("service-history")]
    public async Task<IActionResult> GetServiceHistoryAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetServiceHistoryAsync(request, cancellationToken));

    [HttpGet("song-usage")]
    public async Task<IActionResult> GetSongUsageAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetSongUsageAsync(request, cancellationToken));

    // The four reports below are the Admin's (UC-34 / FE-52); the controller attribute still applies, so this narrows it.
    [HttpGet("user-activity")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetUserActivityAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetUserActivityAsync(request, cancellationToken));

    [HttpGet("rehearsal-attendance")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetRehearsalAttendanceAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetRehearsalAttendanceAsync(request, cancellationToken));

    [HttpGet("participation")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetParticipationAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetParticipationAsync(request, cancellationToken));

    [HttpGet("assignment-completion")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetAssignmentCompletionAsync(
        [FromQuery] SearchReportsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetAssignmentCompletionAsync(request, cancellationToken));
}

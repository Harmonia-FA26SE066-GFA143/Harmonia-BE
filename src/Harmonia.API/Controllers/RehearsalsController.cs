using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Rehearsals and preparation sessions. The Choir Director schedules them (UC-20 / FE-26), takes attendance (UC-30 / FE-45) and sets
/// the songs to practise; every role can read those songs.
/// </summary>
[Route("api/rehearsals")]
[Authorize]
public class RehearsalsController(
    IRehearsalService rehearsalService,
    IRehearsalAttendanceService rehearsalAttendanceService,
    IRehearsalSongService rehearsalSongService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchRehearsalsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalService.SearchAsync(request, cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateRehearsalRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalService.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateAsync(
        Guid id, [FromBody] UpdateRehearsalRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalService.DeleteAsync(id, cancellationToken));

    // Suggestion only, nothing is created; the director edits it and saves through POST or PUT songs.
    [HttpGet("default-songs")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetDefaultSongsAsync(
        [FromQuery] Guid eventId, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalSongService.GetDefaultForEventAsync(eventId, cancellationToken));

    [HttpGet("{id:guid}/songs")]
    public async Task<IActionResult> GetSongsAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalSongService.GetAsync(id, cancellationToken));

    [HttpPut("{id:guid}/songs")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> UpdateSongsAsync(
        Guid id, [FromBody] UpdateRehearsalSongsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalSongService.UpdateAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/attendances")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> GetAttendancesAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalAttendanceService.GetAttendancesAsync(id, cancellationToken));

    [HttpPut("{id:guid}/attendances")]
    [Authorize(Roles = RoleNames.ChoirDirector)]
    public async Task<IActionResult> RecordAttendancesAsync(
        Guid id, [FromBody] RecordRehearsalAttendancesRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await rehearsalAttendanceService.RecordAsync(User.GetUserId(), id, request, cancellationToken));
}
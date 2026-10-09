using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Notes from the Parish Priest to Choir Directors (UC-17 / FE-23): the priest sends, both sides read
/// the notes they sent or received.
/// </summary>
[Route("api/director-notes")]
[Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
public class DirectorNotesController(IDirectorNoteService directorNoteService) : ApiControllerBase
{
    [HttpGet("recipients")]
    [Authorize(Roles = RoleNames.ParishPriest)]
    public async Task<IActionResult> GetRecipientsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.GetRecipientsAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ParishPriest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateDirectorNoteRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.CreateAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchDirectorNotesRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.SearchAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.GetByIdAsync(User.GetUserId(), id, cancellationToken));
}

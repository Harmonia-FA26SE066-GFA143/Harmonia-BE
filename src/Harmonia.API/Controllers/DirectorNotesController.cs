using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>
/// Notes and chat between the Parish Priest and Choir Directors (UC-17 / FE-23): the priest sends notes about a
/// day or event, both sides chat back and forth. New notes and read receipts are pushed over NotificationHub.
/// </summary>
[Route("api/director-notes")]
[Authorize(Roles = $"{RoleNames.ParishPriest},{RoleNames.ChoirDirector}")]
public class DirectorNotesController(IDirectorNoteService directorNoteService) : ApiControllerBase
{
    [HttpGet("recipients")]
    [ProducesResponseType<List<UserSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecipientsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.GetRecipientsAsync(User.GetUserId(), cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleNames.ParishPriest)]
    [ProducesResponseType<List<DirectorNoteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateDirectorNoteRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.CreateAsync(User.GetUserId(), request, cancellationToken));

    [HttpPost("messages")]
    [ProducesResponseType<DirectorNoteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessageAsync(
        [FromBody] SendDirectorNoteMessageRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.SendMessageAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("conversations")]
    [ProducesResponseType<List<DirectorNoteConversationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversationsAsync(CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.GetConversationsAsync(User.GetUserId(), cancellationToken));

    [HttpPut("conversations/{userId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkConversationReadAsync(Guid userId, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.MarkConversationReadAsync(User.GetUserId(), userId, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedList<DirectorNoteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchDirectorNotesRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.SearchAsync(User.GetUserId(), request, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DirectorNoteDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await directorNoteService.GetByIdAsync(User.GetUserId(), id, cancellationToken));
}

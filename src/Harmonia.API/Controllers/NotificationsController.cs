using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMineAsync(
        [FromQuery] PagingRequest paging, CancellationToken cancellationToken) =>
        ToActionResult(await notificationService.GetForUserAsync(User.GetUserId(), paging, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<IActionResult> CountUnreadAsync(CancellationToken cancellationToken) =>
        ToActionResult(await notificationService.CountUnreadAsync(User.GetUserId(), cancellationToken));

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsReadAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await notificationService.MarkAsReadAsync(User.GetUserId(), id, cancellationToken));
}

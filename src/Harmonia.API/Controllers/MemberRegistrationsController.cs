using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Harmonia.API.Controllers;

/// <summary>
/// Requests to join the choir. A visitor sends one from the public home page without signing in; the Admin
/// lists them, creates a Choir Member account from one, or deletes one.
/// </summary>
[Route("api/member-registrations")]
[Authorize(Roles = RoleNames.Admin)]
public class MemberRegistrationsController(IMemberRegistrationService memberRegistrationService) : ApiControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.MemberRegistrationPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubmitAsync(
        [FromBody] CreateMemberRegistrationRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberRegistrationService.SubmitAsync(request, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedList<MemberRegistrationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchMemberRegistrationsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await memberRegistrationService.SearchAsync(request, cancellationToken));

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberRegistrationService.AcceptAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await memberRegistrationService.DeleteAsync(id, cancellationToken));
}

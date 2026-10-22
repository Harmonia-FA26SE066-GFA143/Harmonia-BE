using Harmonia.API.Extensions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using Harmonia.Domain.Common;

namespace Harmonia.API.Controllers;

[Route("api/users")]
[Authorize(Roles = RoleNames.Admin)]
public class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync([FromQuery] SearchUsersRequest request, CancellationToken ct) =>
        ToActionResult(await userService.SearchAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request, CancellationToken ct) =>
        ToActionResult(await userService.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        ToActionResult(await userService.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ActivateAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.ActivateAsync(id, ct));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.DeactivateAsync(id, ct));

    [HttpPut("{id:guid}/role")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignRoleAsync(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct) =>
        ToActionResult(await userService.AssignRoleAsync(id, request, ct));
}
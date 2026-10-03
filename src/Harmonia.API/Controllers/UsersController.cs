using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;

namespace Harmonia.API.Controllers;

[Route("api/users")]
[Authorize(Roles = RoleNames.Admin)]
public class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] SearchUsersRequest request, CancellationToken ct) =>
        ToActionResult(await userService.SearchAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateUserRequest request, CancellationToken ct) =>
        ToActionResult(await userService.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct) =>
        ToActionResult(await userService.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> ActivateAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.ActivateAsync(id, ct));

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateAsync(Guid id, CancellationToken ct) =>
        ToActionResult(await userService.DeactivateAsync(id, ct));

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> AssignRoleAsync(Guid id, [FromBody] AssignRoleRequest request, CancellationToken ct) =>
        ToActionResult(await userService.AssignRoleAsync(id, request, ct));
}
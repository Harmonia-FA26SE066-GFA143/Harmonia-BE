using Harmonia.API.Extensions;
using Harmonia.API.Filters;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService, IUserService userService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(request, cancellationToken));

    [HttpPost("google")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginWithGoogleAsync(
        [FromBody] GoogleLoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginWithGoogleAsync(request, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RefreshTokenAsync(request, cancellationToken));

    [HttpPost("logout")]
    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LogoutAsync(request, cancellationToken));

    [HttpPost("logout-all")]
    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAllAsync(CancellationToken cancellationToken) =>
        ToActionResult(await authService.LogoutAllAsync(User.GetUserId(), cancellationToken));

    [HttpPost("change-password")]
    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>The signed-in user's own account, for every role.</summary>
    [HttpGet("me")]
    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMeAsync(CancellationToken cancellationToken) =>
        ToActionResult(await userService.GetMeAsync(User.GetUserId(), cancellationToken));

    /// <summary>Every role edits their own name and phone.</summary>
    [HttpPut("me")]
    [Authorize]
    [AllowWhenPasswordChangeRequired]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMeAsync(
        [FromBody] UpdateMyUserRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await userService.UpdateMeAsync(User.GetUserId(), request, cancellationToken));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ForgotPasswordAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResetPasswordAsync(request, cancellationToken));
}

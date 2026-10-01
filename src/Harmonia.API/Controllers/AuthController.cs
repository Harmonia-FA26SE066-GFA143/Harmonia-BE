using Harmonia.API.Extensions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginAsync(request, cancellationToken));

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithGoogleAsync(
        [FromBody] GoogleLoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LoginWithGoogleAsync(request, cancellationToken));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.RefreshTokenAsync(request, cancellationToken));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync([FromBody] LogoutRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.LogoutAsync(request, cancellationToken));

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAllAsync(CancellationToken cancellationToken) =>
        ToActionResult(await authService.LogoutAllAsync(User.GetUserId(), cancellationToken));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ForgotPasswordAsync(request, cancellationToken));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await authService.ResetPasswordAsync(request, cancellationToken));
}

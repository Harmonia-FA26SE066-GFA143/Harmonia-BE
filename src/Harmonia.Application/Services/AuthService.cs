using System.Net;
using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Harmonia.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IPasswordHasherService passwordHasherService,
    IEmailSender emailSender,
    IGoogleTokenValidator googleTokenValidator,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IMapper mapper) : IAuthService
{
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasherService.VerifyPassword(user, request.Password))
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthInvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthAccountInactive);
        }

        var response = await IssueTokensAsync(user, request.DeviceId, request.Platform, cancellationToken);
        user.LastLoginAt = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(response);
    }

    public async Task<Result<LoginResponse>> LoginWithGoogleAsync(
        GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var email = await googleTokenValidator.GetVerifiedEmailAsync(request.IdToken, cancellationToken);
        if (email is null)
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthGoogleTokenInvalid);
        }

        // Accounts are created by Admin only: an unknown email gets the same code as a wrong password.
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthInvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthAccountInactive);
        }

        var response = await IssueTokensAsync(user, request.DeviceId, request.Platform, cancellationToken);
        user.LastLoginAt = DateTime.UtcNow;
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(response);
    }

    public async Task<Result<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var existingToken = await userRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            return Result<LoginResponse>.Failure(ErrorCodes.AuthRefreshTokenNotFound);
        }

        existingToken.EnsureUsable();
        existingToken.Revoke();

        if (!existingToken.User.IsActive)
        {
            await userRepository.RevokeAllRefreshTokensAsync(existingToken.UserId, cancellationToken);
            await userRepository.SaveChangesAsync(cancellationToken);
            return Result<LoginResponse>.Failure(ErrorCodes.AuthAccountInactive);
        }

        var response = await IssueTokensAsync(
            existingToken.User, existingToken.DeviceId, existingToken.Platform, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result<LoginResponse>.Success(response);
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var existingToken = await userRepository.GetRefreshTokenByHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            return Result.Failure(ErrorCodes.AuthRefreshTokenNotFound);
        }

        existingToken.Revoke();
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await userRepository.RevokeAllRefreshTokensAsync(userId, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(ErrorCodes.UserNotFound);
        }

        if (!passwordHasherService.VerifyPassword(user, request.CurrentPassword))
        {
            return Result.Failure(ErrorCodes.AuthCurrentPasswordInvalid);
        }

        await SetPasswordAsync(user, request.NewPassword, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Success();
        }

        // Only the newest link works; older ones now read as AUTH_RESET_TOKEN_INVALID.
        await userRepository.RemoveUnusedPasswordResetTokensAsync(user.Id, cancellationToken);

        var rawToken = jwtTokenService.GenerateRefreshToken();
        await userRepository.AddPasswordResetTokenAsync(
            new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = jwtTokenService.HashRefreshToken(rawToken),
                ExpiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime),
            },
            cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

        var options = passwordResetOptions.Value;
        var baseUrl = request.Platform is DevicePlatform.Android or DevicePlatform.iOS
            ? options.MobileUrl
            : options.WebUrl;
        var link = $"{baseUrl}?token={Uri.EscapeDataString(rawToken)}";

        // The result is ignored on purpose: a delivery failure must not reveal that the email exists,
        // and the sender already logs it.
        await emailSender.SendAsync(
            user.Email,
            "Harmonia - Reset your password",
            $"<p>We received a request to reset your Harmonia password.</p>"
                + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset password</a></p>"
                + "<p>This link expires in 1 hour and can be used once. If you did not request it, ignore this email.</p>",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = jwtTokenService.HashRefreshToken(request.Token);
        var resetToken = await userRepository.GetPasswordResetTokenByHashAsync(tokenHash, cancellationToken);
        if (resetToken is null || !resetToken.User.IsActive)
        {
            return Result.Failure(ErrorCodes.AuthResetTokenInvalid);
        }

        var now = DateTime.UtcNow;
        resetToken.EnsureUsable(now);
        resetToken.MarkUsed(now);

        await SetPasswordAsync(resetToken.User, request.NewPassword, cancellationToken);

        return Result.Success();
    }

    /// <summary>Stores the new hash and revokes every refresh token, so all devices must sign in again.</summary>
    private async Task SetPasswordAsync(User user, string newPassword, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHasherService.HashPassword(user, newPassword);
        await userRepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoginResponse> IssueTokensAsync(
        User user, string? deviceId, Domain.Enums.DevicePlatform? platform, CancellationToken cancellationToken)
    {
        var (accessToken, accessTokenExpiresAt) = jwtTokenService.GenerateAccessToken(user);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        await userRepository.AddRefreshTokenAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = jwtTokenService.HashRefreshToken(rawRefreshToken),
                ExpiresAt = jwtTokenService.GetRefreshTokenExpiry(),
                DeviceId = deviceId,
                Platform = platform,
            },
            cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = rawRefreshToken,
            User = mapper.Map<UserSummaryDto>(user),
        };
    }
}

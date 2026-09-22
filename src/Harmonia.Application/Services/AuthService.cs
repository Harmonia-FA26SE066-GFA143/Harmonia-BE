using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IPasswordHasherService passwordHasherService,
    IMapper mapper) : IAuthService
{
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

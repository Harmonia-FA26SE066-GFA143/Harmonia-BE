using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IServices;

public interface IJwtTokenService
{
    /// <summary>Signs a short-lived access token carrying the user id and role claims.</summary>
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);

    /// <summary>Generates a new cryptographically random refresh token (the raw value handed to the client).</summary>
    string GenerateRefreshToken();

    DateTime GetRefreshTokenExpiry();

    /// <summary>Hashes a raw refresh token for storage/lookup. Never store the raw value.</summary>
    string HashRefreshToken(string rawToken);
}

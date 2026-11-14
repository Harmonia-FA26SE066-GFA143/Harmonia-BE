using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Harmonia.Infrastructure.ExternalServices;

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    /// <summary>Present ("true") only while the user still has the password the Admin emailed.</summary>
    public const string PasswordChangeRequiredClaim = "pwd_change_required";

    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.Name),
        };
        if (user.IsPasswordChangeRequired)
        {
            claims.Add(new Claim(PasswordChangeRequiredClaim, "true"));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public DateTime GetRefreshTokenExpiry() => DateTime.UtcNow.AddDays(_options.RefreshTokenExpiryDays);

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

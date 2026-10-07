using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Harmonia.Infrastructure.Tests;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Key = "test-signing-key-that-is-at-least-32-bytes-long!!",
        Issuer = "harmonia-test",
        Audience = "harmonia-test-clients",
        ExpiryMinutes = 30,
        RefreshTokenExpiryDays = 7,
    };

    private readonly JwtTokenService _sut = new(Microsoft.Extensions.Options.Options.Create(Options));

    private static User NewUser() =>
        new() { Id = Guid.NewGuid(), Role = new Role { Name = RoleNames.ChoirDirector } };

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Options.Issuer,
        ValidateAudience = true,
        ValidAudience = Options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.Zero,
    };

    [Fact]
    public void GenerateAccessToken_CarriesUserIdAndRole()
    {
        var user = NewUser();

        var (token, _) = _sut.GenerateAccessToken(user);
        var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(Options.Key), out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(RoleNames.ChoirDirector, principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void GenerateAccessToken_ExpiresAfterConfiguredMinutes()
    {
        var before = DateTime.UtcNow;

        var (token, expiresAt) = _sut.GenerateAccessToken(NewUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.InRange(expiresAt, before.AddMinutes(30), DateTime.UtcNow.AddMinutes(30));
        // JWT "exp" has second precision.
        Assert.True(Math.Abs((jwt.ValidTo - expiresAt.ToUniversalTime()).TotalSeconds) < 1);
    }

    [Fact]
    public void GenerateAccessToken_SignedWithHmacSha256()
    {
        var (token, _) = _sut.GenerateAccessToken(NewUser());

        Assert.Equal(SecurityAlgorithms.HmacSha256, new JwtSecurityTokenHandler().ReadJwtToken(token).Header.Alg);
    }

    [Fact]
    public void GenerateAccessToken_RejectedWithAnotherKey()
    {
        var (token, _) = _sut.GenerateAccessToken(NewUser());

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(
            token, ValidationParameters("another-signing-key-that-is-at-least-32-bytes!!"), out _));
    }

    [Fact]
    public void GenerateRefreshToken_Is64RandomBytes()
    {
        var first = _sut.GenerateRefreshToken();
        var second = _sut.GenerateRefreshToken();

        Assert.Equal(64, Convert.FromBase64String(first).Length);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GetRefreshTokenExpiry_UsesConfiguredDays()
    {
        var before = DateTime.UtcNow;

        var expiry = _sut.GetRefreshTokenExpiry();

        Assert.InRange(expiry, before.AddDays(7), DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public void HashRefreshToken_IsDeterministicSha256Hex()
    {
        var hash = _sut.HashRefreshToken("raw");

        Assert.Equal(hash, _sut.HashRefreshToken("raw"));
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9A-F]+$", hash);
        Assert.NotEqual(hash, _sut.HashRefreshToken("raw2"));
        Assert.DoesNotContain("raw", hash);
    }
}

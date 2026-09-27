using Harmonia.Domain.Entities;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Tests;

public class TokenEntityTests
{
    // ---- RefreshToken ----

    [Fact]
    public void RefreshToken_ActiveAndNotExpired_IsUsable()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddMinutes(5) };

        token.EnsureUsable();
    }

    [Fact]
    public void RefreshToken_Revoked_ThrowsRevoked()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow };

        Assert.Throws<RefreshTokenRevokedException>(token.EnsureUsable);
    }

    [Fact]
    public void RefreshToken_RevokedAndExpired_ReportsRevokedFirst()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddDays(-1), RevokedAt = DateTime.UtcNow };

        Assert.Throws<RefreshTokenRevokedException>(token.EnsureUsable);
    }

    [Fact]
    public void RefreshToken_Expired_ThrowsExpired()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };

        Assert.Throws<RefreshTokenExpiredException>(token.EnsureUsable);
    }

    [Fact]
    public void RefreshToken_Revoke_SetsRevokedAtOnce()
    {
        var firstRevokedAt = DateTime.UtcNow.AddHours(-1);
        var token = new RefreshToken { RevokedAt = firstRevokedAt };

        token.Revoke();

        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }

    [Fact]
    public void RefreshToken_Revoke_Active_SetsRevokedAt()
    {
        var token = new RefreshToken();

        token.Revoke();

        Assert.NotNull(token.RevokedAt);
    }

    // ---- PasswordResetToken ----

    private static readonly DateTime Now = new(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PasswordResetToken_UnusedAndNotExpired_IsUsable()
    {
        var token = new PasswordResetToken { ExpiresAt = Now.AddMinutes(1) };

        token.EnsureUsable(Now);
    }

    [Fact]
    public void PasswordResetToken_Used_ThrowsUsed()
    {
        var token = new PasswordResetToken { ExpiresAt = Now.AddMinutes(30), UsedAt = Now.AddMinutes(-1) };

        Assert.Throws<PasswordResetTokenUsedException>(() => token.EnsureUsable(Now));
    }

    [Fact]
    public void PasswordResetToken_ExactlyAtExpiry_ThrowsExpired()
    {
        var token = new PasswordResetToken { ExpiresAt = Now };

        Assert.Throws<PasswordResetTokenExpiredException>(() => token.EnsureUsable(Now));
    }

    [Fact]
    public void PasswordResetToken_MarkUsed_KeepsFirstUse()
    {
        var token = new PasswordResetToken();

        token.MarkUsed(Now);
        token.MarkUsed(Now.AddMinutes(10));

        Assert.Equal(Now, token.UsedAt);
    }
}

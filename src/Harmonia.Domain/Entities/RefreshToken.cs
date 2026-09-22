using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? DeviceId { get; set; }

    public DevicePlatform? Platform { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Throws if this token can no longer be used to mint a new access token.</summary>
    public void EnsureUsable()
    {
        if (RevokedAt is not null)
        {
            throw new RefreshTokenRevokedException();
        }

        if (ExpiresAt <= DateTime.UtcNow)
        {
            throw new RefreshTokenExpiredException();
        }
    }

    public void Revoke()
    {
        RevokedAt ??= DateTime.UtcNow;
    }
}

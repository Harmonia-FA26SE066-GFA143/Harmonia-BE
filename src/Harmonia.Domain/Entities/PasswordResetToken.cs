using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Entities;

/// <summary>One-time token emailed by forgot-password. Only the hash is stored.</summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Throws if this token can no longer be used to set a new password.</summary>
    public void EnsureUsable(DateTime now)
    {
        if (UsedAt is not null)
        {
            throw new PasswordResetTokenUsedException();
        }

        if (ExpiresAt <= now)
        {
            throw new PasswordResetTokenExpiredException();
        }
    }

    public void MarkUsed(DateTime now)
    {
        UsedAt ??= now;
    }
}

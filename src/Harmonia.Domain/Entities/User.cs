using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class User : BaseAuditableEntity
{
    public string Email { get; set; } = string.Empty;

    // Empty when the Admin left it out; clients then show the email.
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Set when the Admin creates the account and emails the password; cleared once the user sets their own.</summary>
    public bool IsPasswordChangeRequired { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? AvatarUrl { get; set; }

    /// <summary>Storage id used to delete the old avatar when it is replaced.</summary>
    public string? AvatarPublicId { get; set; }

    public Role Role { get; set; } = null!;

    public MemberProfile? MemberProfile { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
}

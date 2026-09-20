using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class User : BaseAuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public Role Role { get; set; } = null!;

    public MemberProfile? MemberProfile { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

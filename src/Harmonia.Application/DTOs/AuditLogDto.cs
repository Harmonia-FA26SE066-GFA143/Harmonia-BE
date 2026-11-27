namespace Harmonia.Application.DTOs;

/// <summary>One entry of the activity history the Admin reviews (UC-35 / FE-54).</summary>
public class AuditLogDto
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Null when nobody was signed in, e.g. a password reset through the emailed link.</summary>
    public Guid? UserId { get; set; }

    public string? UserFullName { get; set; }

    public string? UserEmail { get; set; }

    /// <summary>Created, Updated, Deleted or PasswordChanged.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Name of the changed entity, e.g. User, MemberSkill, SongListReview.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    /// <summary>JSON object of the values before the change; null for Created and PasswordChanged.</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON object of the values after the change; null for Deleted and PasswordChanged.</summary>
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
}

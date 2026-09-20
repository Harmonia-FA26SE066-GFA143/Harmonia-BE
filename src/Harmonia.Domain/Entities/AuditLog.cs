using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
}

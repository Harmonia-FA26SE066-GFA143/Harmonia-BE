using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class NotificationRecipient : BaseEntity
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public Notification Notification { get; set; } = null!;

    public User User { get; set; } = null!;
}

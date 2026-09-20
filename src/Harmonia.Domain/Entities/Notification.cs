using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class Notification : BaseEntity
{
    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<NotificationRecipient> Recipients { get; set; } = [];
}

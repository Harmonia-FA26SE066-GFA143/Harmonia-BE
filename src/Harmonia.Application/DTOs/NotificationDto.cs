using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>What the client receives, both over SignalR and from the notification list endpoint.</summary>
public class NotificationDto
{
    public Guid Id { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}

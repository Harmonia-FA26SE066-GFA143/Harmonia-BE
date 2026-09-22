using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// What a business service hands to <c>INotificationService.SendAsync</c>. Unlike the other
/// Request types it is built in-process rather than bound from a client payload.
/// </summary>
public record SendNotificationRequest(
    NotificationType Type,
    string Title,
    string Content,
    IReadOnlyCollection<Guid> RecipientUserIds,
    string? ReferenceType = null,
    Guid? ReferenceId = null);

namespace Harmonia.Application.DTOs;

public class SendRosterNotificationsRequest
{
    /// <summary>Members to notify, notified again if they already were. Empty: every member with a line not notified yet.</summary>
    public List<Guid> MemberIds { get; set; } = [];
}

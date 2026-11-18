namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of the reject action. The reason is required when a director rejects a member's request, optional when a
/// member declines an invitation; that rule needs the row, so it is checked in ChoirJoinRequestService.
/// </summary>
public class RejectChoirJoinRequestRequest
{
    public string? Reason { get; set; }
}

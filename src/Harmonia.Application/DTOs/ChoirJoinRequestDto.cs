using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class ChoirJoinRequestDto
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public Guid MemberUserId { get; set; }

    public string MemberFullName { get; set; } = string.Empty;

    /// <summary>The choir the member is in now; set on a transfer request, null for a member with no choir.</summary>
    public Guid? CurrentChoirId { get; set; }

    public string? CurrentChoirName { get; set; }

    public Guid ChoirId { get; set; }

    public string ChoirName { get; set; } = string.Empty;

    public bool IsInvitation { get; set; }

    public ChoirJoinRequestStatus Status { get; set; }

    public string? Message { get; set; }

    public string? RejectReason { get; set; }

    public Guid? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

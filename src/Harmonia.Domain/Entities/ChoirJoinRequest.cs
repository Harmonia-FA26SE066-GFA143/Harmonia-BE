using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

/// <summary>
/// A move of a member into a choir that waits for the other side. A member asks to join or transfer and the choir's
/// director (or the Admin) answers; or the director invites a member with no choir and the member answers.
/// A member is in at most one active choir; until approved they stay where they are.
/// </summary>
public class ChoirJoinRequest : BaseAuditableEntity
{
    public Guid MemberId { get; set; }

    public Guid ChoirId { get; set; }

    /// <summary>True when the choir's director (or the Admin) invited the member; false when the member asked.</summary>
    public bool IsInvitation { get; set; }

    public ChoirJoinRequestStatus Status { get; set; }

    /// <summary>Optional note from whoever created it.</summary>
    public string? Message { get; set; }

    /// <summary>Why it was rejected; required when a director rejects a member's request.</summary>
    public string? RejectReason { get; set; }

    /// <summary>User who approved or rejected it; null while Pending or when cancelled.</summary>
    public Guid? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public Choir Choir { get; set; } = null!;

    public User? ReviewedBy { get; set; }
}

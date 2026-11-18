namespace Harmonia.Domain.Enums;

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,

    /// <summary>Was Approved, then withdrawn by a Choir Director; kept as history like Rejected.</summary>
    Revoked
}

namespace Harmonia.Domain.Enums;

public enum ChoirJoinRequestStatus
{
    Pending,
    Approved,
    Rejected,

    /// <summary>Withdrawn by its creator, or closed by the system once the member joined a choir or left the role.</summary>
    Cancelled
}

using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

/// <summary>
/// A Choir Director or Parish Priest puts a member forward to become a Choir Director. The Parish Priest approves it,
/// the member is trained or exempted, and the Admin appoints them to a choir
/// (docs/harmonia-director-nomination-process.md).
/// </summary>
public class DirectorNomination : BaseAuditableEntity
{
    public Guid MemberId { get; set; }

    public Guid NominatedById { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>The choir the nominator suggests; the Admin picks the real one when appointing.</summary>
    public Guid? ProposedChoirId { get; set; }

    public DirectorNominationStatus Status { get; set; }

    /// <summary>Parish Priest who approved or rejected it; null while Pending or when withdrawn before a decision.</summary>
    public Guid? ReviewedById { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Why it was rejected or cancelled.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Set when the Parish Priest skips training, e.g. the member already holds a diocesan certificate.</summary>
    public string? TrainingWaiverReason { get; set; }

    public Guid? AppointedChoirId { get; set; }

    public DateTime? AppointedAt { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public User NominatedBy { get; set; } = null!;

    public User? ReviewedBy { get; set; }

    public Choir? ProposedChoir { get; set; }

    public Choir? AppointedChoir { get; set; }
}

using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MemberSkill : BaseEntity
{
    public Guid MemberId { get; set; }

    public Guid SkillId { get; set; }

    public SkillLevel? Level { get; set; }

    public ApprovalStatus Status { get; set; }

    public DateTime DeclaredAt { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public MemberProfile Member { get; set; } = null!;

    public Skill Skill { get; set; } = null!;

    public User? Approver { get; set; }
}

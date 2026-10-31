using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>A skill declared by the calling member, with its approval outcome.</summary>
public class MemberSkillDto
{
    public Guid Id { get; set; }

    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public SkillLevel? Level { get; set; }

    public ApprovalStatus Status { get; set; }

    public DateTime DeclaredAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }
}

using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>An approved skill as shown in the Choir Director's member list.</summary>
public class MemberSkillSummaryDto
{
    /// <summary>The MemberSkill id, e.g. for PATCH api/member-skills/{id}/revoke.</summary>
    public Guid Id { get; set; }

    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public SkillLevel? Level { get; set; }
}

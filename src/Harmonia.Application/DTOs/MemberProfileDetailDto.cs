using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>GET api/member-profiles/me: the member's own profile with system role and approved, active skills.</summary>
public class MemberProfileDetailDto
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly JoinedDate { get; set; }

    public MemberStatus Status { get; set; }

    public string? AvatarUrl { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public List<MemberSkillSummaryDto> ApprovedSkills { get; set; } = [];
}

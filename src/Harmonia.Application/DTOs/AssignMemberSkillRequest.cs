namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/member-skills/assign: the Choir Director records a skill they have seen themselves.</summary>
public class AssignMemberSkillRequest
{
    public Guid MemberId { get; set; }

    public Guid SkillId { get; set; }
}

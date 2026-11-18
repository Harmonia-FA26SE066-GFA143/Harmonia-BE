namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/member-skills. The member is the caller, never taken from the body.</summary>
public class DeclareMemberSkillRequest
{
    public Guid SkillId { get; set; }
}

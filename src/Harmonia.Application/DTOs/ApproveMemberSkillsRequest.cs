namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/member-skills/bulk-approve: Pending declarations to approve in one go.</summary>
public class ApproveMemberSkillsRequest
{
    public List<Guid> Ids { get; set; } = [];
}

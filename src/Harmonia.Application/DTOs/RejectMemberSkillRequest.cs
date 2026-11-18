namespace Harmonia.Application.DTOs;

/// <summary>Body of PATCH api/member-skills/{id}/reject.</summary>
public class RejectMemberSkillRequest
{
    public string Reason { get; set; } = string.Empty;
}

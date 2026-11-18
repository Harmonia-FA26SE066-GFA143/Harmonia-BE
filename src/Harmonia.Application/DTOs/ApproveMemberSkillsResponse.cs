namespace Harmonia.Application.DTOs;

/// <summary>Each declaration is reviewed on its own: some may pass while others fail.</summary>
public class ApproveMemberSkillsResponse
{
    public List<MemberSkillDetailDto> Approved { get; set; } = [];

    public List<MemberSkillReviewFailureDto> Failed { get; set; } = [];
}

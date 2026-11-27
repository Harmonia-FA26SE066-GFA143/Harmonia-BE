namespace Harmonia.Application.DTOs;

/// <summary>A declaration a bulk review could not act on, with the error code a single review would return.</summary>
public class MemberSkillReviewFailureDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

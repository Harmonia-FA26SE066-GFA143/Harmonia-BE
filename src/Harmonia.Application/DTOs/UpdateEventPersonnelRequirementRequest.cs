namespace Harmonia.Application.DTOs;

public class UpdateEventPersonnelRequirementRequest
{
    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }
}

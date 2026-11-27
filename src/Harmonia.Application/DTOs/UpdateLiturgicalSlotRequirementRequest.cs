namespace Harmonia.Application.DTOs;

public class UpdateLiturgicalSlotRequirementRequest
{
    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }
}

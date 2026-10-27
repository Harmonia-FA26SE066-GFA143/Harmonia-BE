namespace Harmonia.Application.DTOs;

public class LiturgicalSlotRequirementDto
{
    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public Guid SkillCategoryId { get; set; }

    public int RequiredCount { get; set; }
}

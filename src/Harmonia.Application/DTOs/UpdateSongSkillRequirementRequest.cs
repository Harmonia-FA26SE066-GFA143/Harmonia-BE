namespace Harmonia.Application.DTOs;

public class UpdateSongSkillRequirementRequest
{
    public Guid SkillId { get; set; }

    public bool IsMandatory { get; set; }

    public int RequiredCount { get; set; } = 1;
}

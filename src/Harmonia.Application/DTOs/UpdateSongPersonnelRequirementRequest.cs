namespace Harmonia.Application.DTOs;

public class UpdateSongPersonnelRequirementRequest
{
    public Guid SkillId { get; set; }

    public int RequiredCount { get; set; }
}

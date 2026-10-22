namespace Harmonia.Application.DTOs;

public class SongInstrumentRequirementDto
{
    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }
}

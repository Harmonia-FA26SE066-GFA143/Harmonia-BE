namespace Harmonia.Application.DTOs;

/// <summary>A song / skill pair that could not be fully staffed. Computed on the fly, not read from RosterShortage.</summary>
public class RosterShortageDto
{
    public Guid SongListItemId { get; set; }

    public string SongTitle { get; set; } = string.Empty;

    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public int RequiredCount { get; set; }

    public int AssignedCount { get; set; }
}

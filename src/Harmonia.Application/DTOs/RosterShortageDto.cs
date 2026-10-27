namespace Harmonia.Application.DTOs;

/// <summary>
/// A requirement that could not be fully staffed. Computed on the fly, never stored.
/// SongListItemId and SongTitle are null for a whole-service requirement (organist, conductor...).
/// </summary>
public class RosterShortageDto
{
    public Guid? SongListItemId { get; set; }

    public string? SongTitle { get; set; }

    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public int RequiredCount { get; set; }

    public int AssignedCount { get; set; }
}

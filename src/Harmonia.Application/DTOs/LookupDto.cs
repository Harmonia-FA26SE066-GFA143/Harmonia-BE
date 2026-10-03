namespace Harmonia.Application.DTOs;

/// <summary>An active row of a plain lookup table (MassType, CeremonyType, EventCategory, SongTheme, SkillCategory).</summary>
public class LookupDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

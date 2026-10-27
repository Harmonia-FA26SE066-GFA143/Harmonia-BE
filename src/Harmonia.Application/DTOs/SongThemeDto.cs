namespace Harmonia.Application.DTOs;

/// <summary>One node of the theme tree, returned as a flat list; the client nests rows by <see cref="ParentId"/>.</summary>
public class SongThemeDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public Guid? ParentId { get; set; }
}

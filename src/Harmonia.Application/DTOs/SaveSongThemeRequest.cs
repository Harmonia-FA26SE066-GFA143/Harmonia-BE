namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a song theme; <see cref="ParentId"/> null makes it a root of the theme tree.</summary>
public class SaveSongThemeRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ParentId { get; set; }
}

namespace Harmonia.Application.DTOs;

/// <summary>One song on the programme of a rehearsal.</summary>
public class RehearsalSongDto
{
    public Guid SongId { get; set; }

    public string SongTitle { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }
}

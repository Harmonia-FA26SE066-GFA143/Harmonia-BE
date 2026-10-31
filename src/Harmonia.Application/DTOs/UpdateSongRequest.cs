namespace Harmonia.Application.DTOs;

public class UpdateSongRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Composer { get; set; }

    public string? Lyricist { get; set; }

    public string? MusicalKey { get; set; }

    public string? Tempo { get; set; }

    public string? Notes { get; set; }
}

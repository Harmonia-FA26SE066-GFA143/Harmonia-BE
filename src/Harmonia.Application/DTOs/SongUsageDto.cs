namespace Harmonia.Application.DTOs;

/// <summary>How often one song was sung at past events (UC-16 / FE-22).</summary>
public class SongUsageDto
{
    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Composer { get; set; }

    /// <summary>Past events whose approved song list holds the song; a song sung twice in one event counts once.</summary>
    public int TimesUsed { get; set; }

    /// <summary>Null when <see cref="TimesUsed"/> is 0.</summary>
    public DateOnly? LastUsedDate { get; set; }
}

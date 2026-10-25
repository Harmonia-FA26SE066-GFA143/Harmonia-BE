using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class SongDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Composer { get; set; }

    public string? Lyricist { get; set; }

    public string? MusicalKey { get; set; }

    public string? Tempo { get; set; }

    public string? Notes { get; set; }

    /// <summary>Active materials per type; every type is present, 0 when the song has none of it.</summary>
    public Dictionary<MaterialType, int> MaterialCounts { get; set; } = [];

    /// <summary>True once the song has at least one season, Mass type, ceremony type or theme.</summary>
    public bool IsClassified { get; set; }

    /// <summary>Past Published events whose Approved song list holds the song, as in the song-usage report.</summary>
    public int TimesUsed { get; set; }

    /// <summary>Date of the latest of those events; null when <see cref="TimesUsed"/> is 0.</summary>
    public DateOnly? LastUsedDate { get; set; }
}

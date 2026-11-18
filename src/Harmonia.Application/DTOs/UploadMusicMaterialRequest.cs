using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Form fields of the multipart upload; the file itself travels separately as <see cref="Common.Models.FileContent"/>.</summary>
public class UploadMusicMaterialRequest
{
    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    public MaterialType MaterialType { get; set; }

    public Guid? TargetSkillId { get; set; }

    /// <summary>Lyrics only: the text itself, sent instead of a .docx file.</summary>
    public string? LyricsText { get; set; }

    /// <summary>MIDI files only: the instrument they are rendered with. Ignored for any other file.</summary>
    public PlaybackInstrument Instrument { get; set; }
}

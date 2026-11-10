using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class MusicMaterial : BaseAuditableEntity
{
    public Guid SongId { get; set; }

    public MaterialType MaterialType { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Private file: serve through IFileStorageService.GetSignedUrl, never as a stored URL.
    /// Null only for lyrics typed as text (<see cref="LyricsText"/>).
    /// </summary>
    public string? FilePublicId { get; set; }

    public string? FileName { get; set; }

    /// <summary>Lyrics typed in directly; a Lyrics material has either this or a file, never both.</summary>
    public string? LyricsText { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>1 on upload, +1 on every file replacement; lets a client tell files of different revisions apart.</summary>
    public int FileRevision { get; set; } = 1;

    /// <summary>Length of an uploaded .mp3/.m4a/.wav file, read on upload. Null for any other file and for older rows.</summary>
    public double? AudioDurationSeconds { get; set; }

    /// <summary>Audio rendering of a MIDI file, for the mobile piano roll. Null for every non-MIDI material.</summary>
    public string? PlaybackAudioPublicId { get; set; }

    /// <summary>notes.json (schema v1) built on the same timeline as <see cref="PlaybackAudioPublicId"/>.</summary>
    public string? PlaybackNotesPublicId { get; set; }

    /// <summary>
    /// Lowercase hex SHA-256 of the file at <see cref="PlaybackNotesPublicId"/>. Null for rows saved before it existed;
    /// for an interactive score that also means its notes file is schema v1, not the v2 timeline.
    /// </summary>
    public string? PlaybackNotesSha256 { get; set; }

    public double? PlaybackDurationSeconds { get; set; }

    public PlaybackInstrument? PlaybackInstrument { get; set; }

    /// <summary>
    /// Interactive score only: starts at 1 and goes up on every successful file replacement, so a client can tell
    /// a score and a playback of different revisions apart. Null for every other material.
    /// </summary>
    public int? ScoreRevision { get; set; }

    /// <summary>Interactive score only: lowercase hex SHA-256 of the uploaded MusicXML/MXL file.</summary>
    public string? ScoreSha256 { get; set; }

    /// <summary>Interactive score only: lowercase hex SHA-256 of the WAV at <see cref="PlaybackAudioPublicId"/>.</summary>
    public string? PlaybackAudioSha256 { get; set; }

    public Guid? TargetSkillId { get; set; }

    public bool IsActive { get; set; } = true;

    public Song Song { get; set; } = null!;

    public Skill? TargetSkill { get; set; }

    /// <summary>Karaoke timing against this material as the audio side; the lyrics side has no navigation back.</summary>
    public TimedLyrics? TimedLyrics { get; set; }

    public ICollection<MaterialLearningProgress> LearningProgresses { get; set; } = [];
}

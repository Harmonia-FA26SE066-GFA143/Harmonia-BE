using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Harmonia.Domain.ValueObjects;

namespace Harmonia.Domain.Entities;

/// <summary>
/// Karaoke timing of a Lyrics material against one revision of an audio material of the same song. One per audio
/// material; it is removed when that audio file is replaced, so a timing never outlives the audio it was made for.
/// </summary>
public class TimedLyrics : BaseAuditableEntity
{
    public Guid AudioMaterialId { get; set; }

    public Guid LyricsMaterialId { get; set; }

    /// <summary><see cref="MusicMaterial.FileRevision"/> of the audio when the timing was saved.</summary>
    public int AudioRevision { get; set; }

    public TimedLyricsGranularity Granularity { get; set; }

    /// <summary>Stored as one JSON column: always read and replaced as a whole.</summary>
    public List<TimedLyricLine> Lines { get; set; } = [];

    public MusicMaterial AudioMaterial { get; set; } = null!;

    public MusicMaterial LyricsMaterial { get; set; } = null!;
}

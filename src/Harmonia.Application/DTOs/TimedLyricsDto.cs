using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Karaoke timing of a lyrics material against one audio material. Compare <see cref="AudioRevision"/> with the
/// audio's FileRevision (MusicMaterialDto) or Revision (InteractiveScoreDto): if they differ, fetch again.
/// </summary>
public class TimedLyricsDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public Guid LyricsMaterialId { get; set; }

    public Guid AudioMaterialId { get; set; }

    public int AudioRevision { get; set; }

    public TimedLyricsGranularity Granularity { get; set; }

    public List<TimedLyricLineDto> Lines { get; set; } = [];

    public DateTime UpdatedAt { get; set; }
}

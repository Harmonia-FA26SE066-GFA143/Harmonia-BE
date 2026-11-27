using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Replaces the whole timing of an audio material; the Web may build it from an Enhanced LRC file.</summary>
public class UpdateTimedLyricsRequest
{
    public Guid LyricsMaterialId { get; set; }

    public TimedLyricsGranularity Granularity { get; set; } = TimedLyricsGranularity.Line;

    public List<TimedLyricLineRequest> Lines { get; set; } = [];
}

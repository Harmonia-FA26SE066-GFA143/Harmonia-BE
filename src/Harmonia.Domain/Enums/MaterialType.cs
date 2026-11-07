namespace Harmonia.Domain.Enums;

public enum MaterialType
{
    SheetMusic,
    Lyrics,
    SampleAudio,
    RehearsalMaterial,

    /// <summary>MusicXML/MXL score; the server renders its WAV playback from the same file (Web tbd-backlog B47).</summary>
    InteractiveScore
}

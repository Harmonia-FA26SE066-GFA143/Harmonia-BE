namespace Harmonia.Application.Common.Models;

/// <summary>
/// Output of <see cref="Interfaces.IServices.IScoreConverter"/>: the MIDI fed to the renderer and the notes.json
/// schema v2 timeline built from the same ticks and tempo map, so its times match the rendered WAV.
/// </summary>
public record ScoreConversion(byte[] Midi, byte[] TimelineJson);

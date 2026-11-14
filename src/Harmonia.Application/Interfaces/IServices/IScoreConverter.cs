using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Turns a MusicXML score (<c>.musicxml</c>, or the zipped <c>.mxl</c> when <c>isCompressed</c>) into a Standard MIDI
/// File for <see cref="IMidiRenderer"/>, plus the notes.json schema v2 timeline that points every note back at the
/// score. CPU work only, so synchronous like the renderer.
/// Timing follows what the Web's Verovio timemap does by default: repeats are not expanded, grace notes take no time,
/// 120 BPM until the score sets a tempo.
/// MATERIAL_SCORE_INVALID, MATERIAL_SCORE_UNSUPPORTED.
/// </summary>
public interface IScoreConverter
{
    Result<ScoreConversion> ToMidi(Stream score, bool isCompressed);
}

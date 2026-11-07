namespace Harmonia.Application.DTOs;

/// <summary>
/// Advisory score of a recording against the assignment's MIDI reference, computed on request and never stored.
/// Scores are 0–100; <see cref="Score"/> weighs pitch 70% and rhythm 30%.
/// </summary>
/// <param name="CoveragePercent">Share of the reference melody the recording reached; informational, not in the score.</param>
/// <param name="TrackName">The MIDI track (voice part) the recording matched best.</param>
public record PracticeAudioScoreResponse(
    int Score, int PitchScore, int RhythmScore, int CoveragePercent, string TrackName);

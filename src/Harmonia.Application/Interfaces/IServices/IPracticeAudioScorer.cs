using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Scores a sung recording against a MIDI reference by signal analysis: pitch tracking on the recording, then
/// time alignment with the reference notes. <c>notesUrl</c> is the signed URL of the material's notes.json.
/// Returns PRACTICE_AUDIO_UNREADABLE when the audio cannot be decoded or holds too little singing.
/// </summary>
public interface IPracticeAudioScorer
{
    /// <param name="fileName">Only its extension is used, to pick the decoder.</param>
    Task<Result<PracticeAudioScoreResponse>> ScoreAsync(
        Stream audio, string fileName, string notesUrl, CancellationToken cancellationToken);

    /// <summary>Same, for a recording already in storage, read from its signed URL.</summary>
    Task<Result<PracticeAudioScoreResponse>> ScoreAsync(
        string audioUrl, string fileName, string notesUrl, CancellationToken cancellationToken);
}

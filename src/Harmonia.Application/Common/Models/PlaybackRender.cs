namespace Harmonia.Application.Common.Models;

/// <summary>
/// Output of <see cref="Interfaces.IServices.IMidiRenderer"/>: notes.json and a WAV file built from one timeline,
/// so the mobile piano roll stays in sync with the audio.
/// </summary>
public record PlaybackRender(byte[] NotesJson, byte[] AudioWav, double DurationSeconds);

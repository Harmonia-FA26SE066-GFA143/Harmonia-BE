using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Turns a Standard MIDI File into an audio rendering, every channel on <c>instrument</c>, for the mobile piano roll. CPU work only, no I/O,
/// so it is synchronous like <see cref="IReportFileWriter"/>.
/// MIDI_INVALID_FILE, MIDI_FORMAT_UNSUPPORTED, MIDI_NO_PLAYABLE_NOTES, MIDI_LIMIT_EXCEEDED.
/// </summary>
public interface IMidiRenderer
{
    Result<PlaybackRender> Render(Stream midi, PlaybackInstrument instrument);
}

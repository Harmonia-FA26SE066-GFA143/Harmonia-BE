namespace Harmonia.Domain.Enums;

/// <summary>
/// Instrument a MIDI material is rendered with for the mobile piano roll; every channel plays it.
/// Piano is first so a form that omits the field gets it.
/// </summary>
public enum PlaybackInstrument
{
    Piano,
    Organ,
    Guitar,
    StringEnsemble
}

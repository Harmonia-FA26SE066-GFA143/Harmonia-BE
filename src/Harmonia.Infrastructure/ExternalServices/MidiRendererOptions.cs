namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>Bound from the optional "MidiRenderer" configuration section; the defaults work without it.</summary>
public class MidiRendererOptions
{
    public const string SectionName = "MidiRenderer";

    /// <summary>
    /// General MIDI SoundFont (.sf2) with a licence that allows use in the product. Relative paths start at the app's
    /// output folder, where src/Harmonia.API/SoundFonts/ is copied on build and publish.
    /// </summary>
    public string SoundFontPath { get; set; } = "SoundFonts/GeneralUser-GS.sf2";
}

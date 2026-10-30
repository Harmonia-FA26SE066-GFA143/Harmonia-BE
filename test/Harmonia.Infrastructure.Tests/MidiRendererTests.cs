using System.Text.Json;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.ExternalServices;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.Tests;

/// <summary>
/// Rejection paths return before the SoundFont loads. The rendering tests use the committed
/// src/Harmonia.API/SoundFonts/GeneralUser-GS.sf2, the file production renders with.
/// </summary>
public class MidiRendererTests
{
    private readonly MidiRenderer _sut = new(Options.Create(new MidiRendererOptions()));

    private static MemoryStream Write(MidiFile file)
    {
        var stream = new MemoryStream();
        file.Write(stream);
        stream.Position = 0;
        return stream;
    }

    private static NoteOnEvent On(int channel, long delta = 0) =>
        new((SevenBitNumber)60, (SevenBitNumber)90) { Channel = (FourBitNumber)channel, DeltaTime = delta };

    private static NoteOffEvent Off(int channel, long delta) =>
        new((SevenBitNumber)60, (SevenBitNumber)0) { Channel = (FourBitNumber)channel, DeltaTime = delta };

    [Fact]
    public void Render_NotAMidiFile_ReturnsInvalidFile()
    {
        var result = _sut.Render(new MemoryStream("not a midi file"u8.ToArray()), PlaybackInstrument.Piano);

        Assert.Equal(ErrorCodes.MidiInvalidFile, result.Code);
    }

    [Fact]
    public void Render_OnlyPercussion_ReturnsNoPlayableNotes()
    {
        var result = _sut.Render(Write(new MidiFile(new TrackChunk(On(9), Off(9, 480)))), PlaybackInstrument.Piano);

        Assert.Equal(ErrorCodes.MidiNoPlayableNotes, result.Code);
    }

    [Fact]
    public void Render_NoteOnWithoutNoteOff_ReturnsInvalidFile()
    {
        var result = _sut.Render(Write(new MidiFile(new TrackChunk(On(0)))), PlaybackInstrument.Piano);

        Assert.Equal(ErrorCodes.MidiInvalidFile, result.Code);
    }

    [Fact]
    public void Render_SmpteTimeDivision_ReturnsFormatUnsupported()
    {
        var file = new MidiFile(new TrackChunk(On(0), Off(0, 480)))
        {
            TimeDivision = new SmpteTimeDivision(SmpteFormat.TwentyFive, 40),
        };

        var result = _sut.Render(Write(file), PlaybackInstrument.Piano);

        Assert.Equal(ErrorCodes.MidiFormatUnsupported, result.Code);
    }

    [Fact]
    public void Render_LongerThanTheLimit_ReturnsLimitExceeded()
    {
        // 480 ticks per quarter at the default 120 BPM is 0.5 s, so this note ends past ten minutes.
        var ticks = (long)(MidiRenderer.MaxTimelineSeconds * 2 * 480) + 480;

        var result = _sut.Render(Write(new MidiFile(new TrackChunk(On(0), Off(0, ticks)))
        {
            TimeDivision = new TicksPerQuarterNoteTimeDivision(480),
        }), PlaybackInstrument.Piano);

        Assert.Equal(ErrorCodes.MidiLimitExceeded, result.Code);
    }

    private static MidiRenderer RendererWithCommittedSoundFont()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "Harmonia.Solution.slnx"))) root = root.Parent!;

        // Path.Combine ignores the base directory when the second path is absolute.
        return new MidiRenderer(Options.Create(new MidiRendererOptions
        {
            SoundFontPath = Path.Combine(root.FullName, "src", "Harmonia.API", "SoundFonts", "GeneralUser-GS.sf2"),
        }));
    }

    [Theory]
    [InlineData(PlaybackInstrument.Piano)]
    [InlineData(PlaybackInstrument.Organ)]
    [InlineData(PlaybackInstrument.Guitar)]
    [InlineData(PlaybackInstrument.StringEnsemble)]
    public void Render_TempoChange_NotesAndAudioShareTheTimeline(PlaybackInstrument instrument)
    {
        // 480 ticks per quarter: the first note lasts 960 ticks at 120 BPM (1 s), then the tempo halves
        // and the second note lasts 480 ticks at 60 BPM (1 s more).
        var tempo = new TrackChunk(
            new SetTempoEvent(500_000),
            new SetTempoEvent(1_000_000) { DeltaTime = 960 });
        var melody = new TrackChunk(
            new SequenceTrackNameEvent("Melody"),
            new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)90),
            new NoteOffEvent((SevenBitNumber)60, (SevenBitNumber)0) { DeltaTime = 960 },
            new NoteOnEvent((SevenBitNumber)64, (SevenBitNumber)80),
            new NoteOffEvent((SevenBitNumber)64, (SevenBitNumber)0) { DeltaTime = 480 });
        var file = new MidiFile(tempo, melody) { TimeDivision = new TicksPerQuarterNoteTimeDivision(480) };

        var result = RendererWithCommittedSoundFont().Render(Write(file), instrument);

        Assert.True(result.IsSuccess, result.Code);
        using var json = JsonDocument.Parse(result.Value!.NotesJson);
        var notes = json.RootElement.GetProperty("notes").EnumerateArray().ToArray();
        Assert.Equal(2, notes.Length);
        Assert.Equal(1.0, notes[1].GetProperty("start").GetDouble(), 3);
        Assert.Equal(1.0, notes[1].GetProperty("duration").GetDouble(), 3);
        Assert.Equal(2.0, json.RootElement.GetProperty("notesEndSeconds").GetDouble(), 3);
        Assert.Equal("Melody", json.RootElement.GetProperty("tracks")[0].GetProperty("name").GetString());

        // Two seconds of notes plus the two-second tail, as 16-bit mono after a 44-byte header.
        Assert.Equal(4.0, result.Value.DurationSeconds, 3);
        Assert.Equal(44 + (int)(4.0 * MidiRenderer.SampleRate) * 2, result.Value.AudioWav.Length);
        Assert.Equal(result.Value.DurationSeconds, json.RootElement.GetProperty("durationSeconds").GetDouble());

        // The first half second must actually sound, not be silence.
        var samples = new ReadOnlySpan<byte>(result.Value.AudioWav, 44, MidiRenderer.SampleRate);
        var peak = 0;
        for (var i = 0; i < samples.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(samples[i..])));
        Assert.True(peak > 1000, $"Audio is near silent (peak {peak}).");
    }
}

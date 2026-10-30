using Harmonia.Domain.Common;
using Harmonia.Infrastructure.ExternalServices;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.Tests;

/// <summary>
/// Rejection paths only: each returns before the SoundFont is loaded, so no .sf2 is needed here.
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
        var result = _sut.Render(new MemoryStream("not a midi file"u8.ToArray()));

        Assert.Equal(ErrorCodes.MidiInvalidFile, result.Code);
    }

    [Fact]
    public void Render_OnlyPercussion_ReturnsNoPlayableNotes()
    {
        var result = _sut.Render(Write(new MidiFile(new TrackChunk(On(9), Off(9, 480)))));

        Assert.Equal(ErrorCodes.MidiNoPlayableNotes, result.Code);
    }

    [Fact]
    public void Render_NoteOnWithoutNoteOff_ReturnsInvalidFile()
    {
        var result = _sut.Render(Write(new MidiFile(new TrackChunk(On(0)))));

        Assert.Equal(ErrorCodes.MidiInvalidFile, result.Code);
    }

    [Fact]
    public void Render_SmpteTimeDivision_ReturnsFormatUnsupported()
    {
        var file = new MidiFile(new TrackChunk(On(0), Off(0, 480)))
        {
            TimeDivision = new SmpteTimeDivision(SmpteFormat.TwentyFive, 40),
        };

        var result = _sut.Render(Write(file));

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
        }));

        Assert.Equal(ErrorCodes.MidiLimitExceeded, result.Code);
    }
}

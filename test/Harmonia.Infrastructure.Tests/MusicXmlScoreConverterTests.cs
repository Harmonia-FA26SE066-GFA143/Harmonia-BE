using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Harmonia.Application.Common.Models;
using Harmonia.Domain.Common;
using Harmonia.Infrastructure.ExternalServices;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Harmonia.Infrastructure.Tests;

public class MusicXmlScoreConverterTests
{
    private readonly MusicXmlScoreConverter _sut = new();

    private const string Doctype =
        """<!DOCTYPE score-partwise PUBLIC "-//Recordare//DTD MusicXML 4.0 Partwise//EN" "http://www.musicxml.org/dtds/partwise.dtd">""";

    // One part, divisions 1 (a quarter is duration 1), measures given as raw MusicXML.
    private static string Score(params string[] measures) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        {Doctype}
        <score-partwise version="4.0">
          <part-list><score-part id="P1"><part-name>Soprano</part-name></score-part></part-list>
          <part id="P1">
            {string.Concat(measures.Select((m, i) => $"<measure number=\"{i + 1}\">{(i == 0 ? "<attributes><divisions>1</divisions></attributes>" : "")}{m}</measure>"))}
          </part>
        </score-partwise>
        """;

    private static string Note(string step, int octave, int duration, string extra = "") =>
        $"<note>{extra}<pitch><step>{step}</step><octave>{octave}</octave></pitch><duration>{duration}</duration></note>";

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    private static List<(int Pitch, double Start, double Length)> Notes(ScoreConversion conversion)
    {
        var file = MidiFile.Read(new MemoryStream(conversion.Midi));
        var tempoMap = file.GetTempoMap();
        double Seconds(long ticks) => TimeConverter.ConvertTo<MetricTimeSpan>(ticks, tempoMap).TotalMicroseconds / 1_000_000d;
        return file.GetNotes()
            .Select(n => ((int)n.NoteNumber, Math.Round(Seconds(n.Time), 3), Math.Round(Seconds(n.EndTime) - Seconds(n.Time), 3)))
            .ToList();
    }

    private static MemoryStream Mxl(params (string Path, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ToMidi_TwoQuarters_DefaultTempoGivesHalfSecondEach()
    {
        var result = _sut.ToMidi(Utf8(Score(Note("C", 4, 1) + Note("D", 4, 1))), isCompressed: false);

        Assert.True(result.IsSuccess);
        Assert.Equal([(60, 0, 0.5), (62, 0.5, 0.5)], Notes(result.Value!));
    }

    [Fact]
    public void ToMidi_SoundTempo_SetsTheTimeline()
    {
        var measure = "<direction><sound tempo=\"60\"/></direction>" + Note("C", 4, 1) + Note("C", 4, 1);

        var result = _sut.ToMidi(Utf8(Score(measure)), isCompressed: false);

        Assert.Equal([(60, 0, 1), (60, 1, 1)], Notes(result.Value!));
    }

    [Fact]
    public void ToMidi_ChordTieGraceRestAndBackup_FollowTheWrittenTimeline()
    {
        var voice1 = Note("C", 4, 1, "<grace/>") // no time
            + Note("C", 4, 1) + Note("E", 4, 1, "<chord/>")
            + Note("G", 4, 1, "<tie type=\"start\"/>") + Note("G", 4, 1, "<tie type=\"stop\"/>")
            + "<note><rest/><duration>1</duration></note>";
        var voice2 = "<backup><duration>4</duration></backup>" + Note("C", 3, 4);

        var result = _sut.ToMidi(Utf8(Score(voice1 + voice2, Note("A", 4, 1))), isCompressed: false);

        // Bar 1 is four quarters (2 s): C4+E4 chord, G4 tied over two quarters, a rest; C3 under it in voice 2.
        Assert.Equal(
            [(48, 0, 2), (60, 0, 0.5), (64, 0, 0.5), (67, 0.5, 1), (69, 2, 0.5)],
            Notes(result.Value!).OrderBy(n => n.Start).ThenBy(n => n.Pitch).ToList());
    }

    [Fact]
    public void ToMidi_Timeline_LocatesEveryNoteInTheScoreAndTimesEachMeasure()
    {
        var voice1 = Note("C", 4, 1, "<grace/>") // index 0, no time, not listed
            + Note("C", 4, 1, "<voice>1</voice>") + Note("E", 4, 1, "<chord/><voice>1</voice>")
            + Note("G", 4, 1, "<tie type=\"start\"/><voice>1</voice>") + Note("G", 4, 1, "<tie type=\"stop\"/><voice>1</voice>")
            + "<note><rest/><duration>1</duration></note>";
        var voice2 = "<backup><duration>4</duration></backup>" + Note("C", 3, 4, "<voice>2</voice><staff>2</staff>");

        var result = _sut.ToMidi(Utf8(Score(voice1 + voice2, Note("A", 4, 1))), isCompressed: false);

        using var json = JsonDocument.Parse(result.Value!.TimelineJson);
        var root = json.RootElement;
        Assert.Equal((2, "written"), (root.GetProperty("schemaVersion").GetInt32(), root.GetProperty("readingOrder").GetString()));

        var notes = root.GetProperty("notes").EnumerateArray()
            .Select(n => (Id: n.GetProperty("id").GetString(), Voice: n.GetProperty("voice").GetString(),
                Staff: n.GetProperty("staff").GetInt32(), Pitch: n.GetProperty("pitch").GetInt32(),
                Start: n.GetProperty("start").GetDouble(), Duration: n.GetProperty("duration").GetDouble()))
            .ToList();
        // The tied G is one note at its first <note> (index 3); same order and times as the v1 renderer would give.
        Assert.Equal(
            [("P1:0:6", "2", 2, 48, 0, 2), ("P1:0:1", "1", 1, 60, 0, 0.5), ("P1:0:2", "1", 1, 64, 0, 0.5),
             ("P1:0:3", "1", 1, 67, 0.5, 1), ("P1:1:0", "1", 1, 69, 2, 0.5)],
            notes);

        var measures = root.GetProperty("measures").EnumerateArray()
            .Select(m => (m.GetProperty("measureIndex").GetInt32(), m.GetProperty("measureNumber").GetString(),
                m.GetProperty("start").GetDouble(), m.GetProperty("end").GetDouble()))
            .ToList();
        Assert.Equal([(0, "1", 0, 2), (1, "2", 2, 2.5)], measures);
        Assert.Equal("Soprano", root.GetProperty("tracks")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void ToMidi_ExternalEntity_ReturnsInvalidAndReadsNothing()
    {
        var xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE score-partwise [<!ENTITY secret SYSTEM "file:///c:/windows/win.ini">]>
            <score-partwise><part-list/><part id="P1"><measure><note><pitch><step>&secret;</step></pitch></note></measure></part></score-partwise>
            """;

        var result = _sut.ToMidi(Utf8(xxe), isCompressed: false);

        Assert.Equal(ErrorCodes.MaterialScoreInvalid, result.Code);
    }

    [Fact]
    public void ToMidi_NotXml_ReturnsInvalid()
    {
        Assert.Equal(ErrorCodes.MaterialScoreInvalid, _sut.ToMidi(Utf8("not xml"), isCompressed: false).Code);
    }

    [Fact]
    public void ToMidi_Timewise_ReturnsUnsupported()
    {
        var result = _sut.ToMidi(Utf8("<score-timewise version=\"4.0\"/>"), isCompressed: false);

        Assert.Equal(ErrorCodes.MaterialScoreUnsupported, result.Code);
    }

    [Fact]
    public void ToMidi_OnlyRests_ReturnsUnsupported()
    {
        var result = _sut.ToMidi(Utf8(Score("<note><rest/><duration>4</duration></note>")), isCompressed: false);

        Assert.Equal(ErrorCodes.MaterialScoreUnsupported, result.Code);
    }

    [Fact]
    public void ToMidi_BadOctave_ReturnsInvalid()
    {
        var result = _sut.ToMidi(Utf8(Score("<note><pitch><step>C</step><octave>x</octave></pitch><duration>1</duration></note>")), isCompressed: false);

        Assert.Equal(ErrorCodes.MaterialScoreInvalid, result.Code);
    }

    [Fact]
    public void ToMidi_MxlWithContainer_ReadsTheRootFile()
    {
        var container = """
            <container><rootfiles><rootfile full-path="scores/song.musicxml"/></rootfiles></container>
            """;
        var mxl = Mxl(
            ("META-INF/container.xml", Encoding.UTF8.GetBytes(container)),
            ("scores/song.musicxml", Encoding.UTF8.GetBytes(Score(Note("C", 4, 1)))));

        var result = _sut.ToMidi(mxl, isCompressed: true);

        Assert.Equal([(60, 0, 0.5)], Notes(result.Value!));
    }

    [Fact]
    public void ToMidi_MxlPastTheDecompressedLimit_ReturnsInvalid()
    {
        // Zeros deflate to a few kilobytes: the archive is small, the inflated score is not.
        var mxl = Mxl(("song.xml", new byte[MusicXmlScoreConverter.MaxDecompressedBytes + 1]));

        var result = _sut.ToMidi(mxl, isCompressed: true);

        Assert.Equal(ErrorCodes.MaterialScoreInvalid, result.Code);
    }

    [Fact]
    public void ToMidi_MxlWithTooManyEntries_ReturnsInvalid()
    {
        var entries = Enumerable.Range(0, MusicXmlScoreConverter.MaxZipEntries + 1)
            .Select(i => ($"f{i}.txt", new byte[1])).ToArray();

        Assert.Equal(ErrorCodes.MaterialScoreInvalid, _sut.ToMidi(Mxl(entries), isCompressed: true).Code);
    }

    [Fact]
    public void ToMidi_NotAZip_ReturnsInvalid()
    {
        Assert.Equal(ErrorCodes.MaterialScoreInvalid, _sut.ToMidi(Utf8("not a zip"), isCompressed: true).Code);
    }
}

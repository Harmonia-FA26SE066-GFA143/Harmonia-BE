using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Reads score-partwise MusicXML and writes one MIDI track per part, walking every measure once in written order,
/// plus a notes.json schema v2 timeline from the same ticks: every note carries the part, measure, voice, staff and
/// index of its &lt;note&gt; element, and each part has a measure timeline for the Web cursor.
/// Read safely: DTDs are ignored and nothing is resolved (no XXE, no entity expansion); an MXL is read in memory only
/// (no paths on disk, so no zip-slip) with caps on entries and decompressed bytes.
/// </summary>
public class MusicXmlScoreConverter : IScoreConverter
{
    public const int MaxZipEntries = 100;

    public const long MaxDecompressedBytes = 50L * 1024 * 1024;

    private const int TicksPerQuarter = 960;

    private const int Velocity = 80;

    private const int PercussionChannel = 9;

    private static readonly int[] StepSemitones = [9, 11, 0, 2, 4, 5, 7]; // A B C D E F G

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Result<ScoreConversion> ToMidi(Stream score, bool isCompressed)
    {
        XDocument document;
        try
        {
            document = isCompressed ? LoadMxl(score) : Load(score);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            return Result<ScoreConversion>.Failure(ErrorCodes.MaterialScoreInvalid);
        }

        var root = document.Root!;
        if (root.Name.LocalName != "score-partwise") return Result<ScoreConversion>.Failure(ErrorCodes.MaterialScoreUnsupported);

        try
        {
            return Convert(root);
        }
        catch (FormatException)
        {
            return Result<ScoreConversion>.Failure(ErrorCodes.MaterialScoreInvalid);
        }
    }

    private static Result<ScoreConversion> Convert(XElement root)
    {
        var names = root.Element("part-list")?.Elements("score-part")
            .ToDictionary(x => (string?)x.Attribute("id") ?? "", x => x.Element("part-name")?.Value.Trim());

        var tempos = new SortedDictionary<long, double>();
        var tracks = new List<TrackChunk>();
        var parts = new List<PartTimeline>();
        foreach (var part in root.Elements("part"))
        {
            var channel = tracks.Count % 15;
            if (channel >= PercussionChannel) channel++;
            var id = (string?)part.Attribute("id") ?? "";
            var (notes, measures) = ReadPart(part, (FourBitNumber)channel, tempos);

            var chunk = notes.Select(x => x.Note).Cast<ITimedObject>().ToTrackChunk();
            var name = names?.GetValueOrDefault(id);
            if (string.IsNullOrEmpty(name)) name = $"Part {tracks.Count + 1}";
            chunk.Events.Insert(0, new SequenceTrackNameEvent(name));
            tracks.Add(chunk);
            parts.Add(new PartTimeline(id, name, notes, measures));
        }

        if (parts.All(x => x.Notes.Count == 0)) return Result<ScoreConversion>.Failure(ErrorCodes.MaterialScoreUnsupported);

        var file = new MidiFile(tracks) { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        using (var tempoManager = new TempoMapManager(file.TimeDivision))
        {
            foreach (var (tick, bpm) in tempos) tempoManager.SetTempo(tick, Tempo.FromBeatsPerMinute(bpm));
            file.ReplaceTempoMap(tempoManager.TempoMap);
        }

        using var output = new MemoryStream();
        file.Write(output);
        return Result<ScoreConversion>.Success(new ScoreConversion(output.ToArray(), Timeline(parts, file.GetTempoMap())));
    }

    /// <summary>
    /// Superset of the MidiRenderer schema v1 (same pitch/start/duration/velocity/track and track numbering), so a v1
    /// client still reads it. A tied note appears once, located at its first written &lt;note&gt;.
    /// </summary>
    private static byte[] Timeline(List<PartTimeline> parts, TempoMap tempoMap)
    {
        double Seconds(long ticks) => TimeConverter.ConvertTo<MetricTimeSpan>(ticks, tempoMap).TotalMicroseconds / 1_000_000d;

        var tracks = new List<object>();
        var measures = new List<object>();
        var notes = new List<(double Start, double Duration, int Track, PartTimeline Part, Note Note, NoteSource Source)>();
        foreach (var part in parts)
        {
            // MidiRenderer skips tracks with no notes, so this numbering matches its "track".
            if (part.Notes.Count == 0) continue;
            var track = tracks.Count;
            tracks.Add(new { id = track, name = part.Name, partId = part.Id });
            measures.AddRange(part.Measures.Select(m => new
            {
                partId = part.Id,
                measureIndex = m.Index,
                measureNumber = m.Number,
                start = Math.Round(Seconds(m.StartTick), 4),
                end = Math.Round(Seconds(m.EndTick), 4),
            }));
            foreach (var (note, source) in part.Notes)
            {
                var start = Seconds(note.Time);
                notes.Add((start, Seconds(note.EndTime) - start, track, part, note, source));
            }
        }

        var notesEnd = notes.Max(n => n.Start + n.Duration);
        notes.Sort((a, b) => (a.Start, a.Track, (int)a.Note.NoteNumber, a.Source.MeasureIndex, a.Source.NoteIndex)
            .CompareTo((b.Start, b.Track, (int)b.Note.NoteNumber, b.Source.MeasureIndex, b.Source.NoteIndex)));
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            timeUnit = "seconds",
            durationSeconds = MidiRenderer.TotalDurationSeconds(notesEnd),
            notesEndSeconds = Math.Round(notesEnd, 4),
            minPitch = notes.Min(n => (int)n.Note.NoteNumber),
            maxPitch = notes.Max(n => (int)n.Note.NoteNumber),
            // Every measure once, in written order: repeats, voltas, D.C. and D.S. are not expanded.
            readingOrder = "written",
            tracks,
            measures,
            notes = notes.Select(n => new
            {
                id = $"{n.Part.Id}:{n.Source.MeasureIndex}:{n.Source.NoteIndex}",
                partId = n.Part.Id,
                partName = n.Part.Name,
                measureIndex = n.Source.MeasureIndex,
                measureNumber = n.Source.MeasureNumber,
                voice = n.Source.Voice,
                staff = n.Source.Staff,
                noteIndex = n.Source.NoteIndex,
                pitch = (int)n.Note.NoteNumber,
                start = Math.Round(n.Start, 4),
                duration = Math.Round(n.Duration, 4),
                velocity = (int)n.Note.Velocity,
                track = n.Track,
            }),
        }, JsonOptions);
    }

    /// <summary>Where a note sits in the MusicXML: <c>NoteIndex</c> counts every &lt;note&gt; element of the measure from 0.</summary>
    private sealed record NoteSource(int MeasureIndex, string? MeasureNumber, string Voice, int Staff, int NoteIndex);

    private sealed record MeasureSpan(int Index, string? Number, long StartTick, long EndTick);

    private sealed record PartTimeline(string Id, string Name, List<(Note Note, NoteSource Source)> Notes, List<MeasureSpan> Measures);

    /// <summary>Positions are counted in quarter notes, so a change of divisions mid-part keeps the timeline.</summary>
    private static (List<(Note Note, NoteSource Source)> Notes, List<MeasureSpan> Measures) ReadPart(
        XElement part, FourBitNumber channel, SortedDictionary<long, double> tempos)
    {
        var notes = new List<(Note Note, NoteSource Source)>();
        var measures = new List<MeasureSpan>();
        var tied = new Dictionary<int, Note>(); // pitch → note whose tie is still open
        double divisions = 1, measureStart = 0;
        long Tick(double quarters) => (long)Math.Round(quarters * TicksPerQuarter);

        foreach (var measure in part.Elements("measure"))
        {
            double position = measureStart, measureEnd = measureStart, lastStart = measureStart;
            var number = (string?)measure.Attribute("number");
            var noteIndex = -1;
            foreach (var element in measure.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "attributes" when element.Element("divisions") is { } d:
                        divisions = Number(d.Value);
                        if (divisions <= 0) throw new FormatException("Divisions must be positive.");
                        break;
                    case "backup":
                        position -= Duration(element, divisions);
                        break;
                    case "forward":
                        position += Duration(element, divisions);
                        break;
                    case "sound" or "direction":
                        var tempo = (string?)(element.Name.LocalName == "sound" ? element : element.Element("sound"))?.Attribute("tempo");
                        if (tempo is not null && Number(tempo) > 0) tempos[Tick(position)] = Number(tempo);
                        break;
                    case "note":
                        noteIndex++;
                        if (element.Element("grace") is not null) break;
                        var duration = Duration(element, divisions);
                        var start = element.Element("chord") is not null ? lastStart : position;
                        if (element.Element("chord") is null) position += duration;
                        lastStart = start;
                        if (element.Element("pitch") is { } pitch && element.Element("cue") is null
                            && AddNote(tied, element, Pitch(pitch), Tick(start), Tick(start + duration), channel) is { } note)
                        {
                            var staff = element.Element("staff") is { } st ? (int)Number(st.Value) : 1;
                            var voice = element.Element("voice")?.Value.Trim() is { Length: > 0 } v ? v : "1";
                            notes.Add((note, new NoteSource(measures.Count, number, voice, staff, noteIndex)));
                        }

                        break;
                }

                measureEnd = Math.Max(measureEnd, position);
            }

            measures.Add(new MeasureSpan(measures.Count, number, Tick(measureStart), Tick(measureEnd)));
            measureStart = measureEnd;
        }

        return (notes, measures);
    }

    /// <summary>A new note, or null when the element only extends a tied note (or takes no time).</summary>
    private static Note? AddNote(
        Dictionary<int, Note> tied, XElement element, int pitch, long start, long end, FourBitNumber channel)
    {
        Note? created = null;
        var ties = element.Elements("tie").Select(x => (string?)x.Attribute("type")).ToList();
        if (ties.Contains("stop") && tied.TryGetValue(pitch, out var open))
        {
            open.Length = Math.Max(open.Length, end - open.Time);
        }
        else if (end > start)
        {
            open = created = new Note((SevenBitNumber)pitch, end - start, start) { Channel = channel, Velocity = (SevenBitNumber)Velocity };
        }
        else return null;

        if (ties.Contains("start")) tied[pitch] = open;
        else tied.Remove(pitch);
        return created;
    }

    private static int Pitch(XElement pitch)
    {
        var step = (pitch.Element("step")?.Value.Trim() ?? "") switch
        {
            [var c and >= 'A' and <= 'G'] => StepSemitones[c - 'A'],
            _ => throw new FormatException("Invalid pitch step."),
        };
        var alter = pitch.Element("alter") is { } a ? (int)Math.Round(Number(a.Value)) : 0;
        var midi = (int)(Number(pitch.Element("octave")?.Value ?? "") + 1) * 12 + step + alter;
        return midi is >= 0 and <= 127 ? midi : throw new FormatException("Pitch out of MIDI range.");
    }

    private static double Duration(XElement element, double divisions)
    {
        var duration = element.Element("duration") is { } d ? Number(d.Value) : 0;
        return duration >= 0 ? duration / divisions : throw new FormatException("Negative duration.");
    }

    private static double Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number
            : throw new FormatException("Invalid number.");

    private static XDocument Load(Stream xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDecompressedBytes,
        };
        using var reader = XmlReader.Create(xml, settings);
        return XDocument.Load(reader);
    }

    private static XDocument LoadMxl(Stream mxl)
    {
        using var zip = new ZipArchive(mxl, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > MaxZipEntries) throw new InvalidDataException("Too many entries.");

        var budget = MaxDecompressedBytes;
        var rootPath = zip.GetEntry("META-INF/container.xml") is { } container
            ? (string?)Load(ReadBounded(container, ref budget)).Descendants("rootfile").FirstOrDefault()?.Attribute("full-path")
            : null;
        var entry = (rootPath is null ? null : zip.GetEntry(rootPath))
            ?? zip.Entries.FirstOrDefault(x => !x.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)
                && (x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                    || x.FullName.EndsWith(".musicxml", StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidDataException("No score in the archive.");
        return Load(ReadBounded(entry, ref budget));
    }

    // Counts the bytes actually inflated, against one budget for the whole archive: declared sizes can lie.
    private static MemoryStream ReadBounded(ZipArchiveEntry entry, ref long budget)
    {
        var output = new MemoryStream();
        using var input = entry.Open();
        var buffer = new byte[81920];
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            budget -= read;
            if (budget < 0) throw new InvalidDataException("Decompressed score too large.");
            output.Write(buffer, 0, read);
        }

        output.Position = 0;
        return output;
    }
}

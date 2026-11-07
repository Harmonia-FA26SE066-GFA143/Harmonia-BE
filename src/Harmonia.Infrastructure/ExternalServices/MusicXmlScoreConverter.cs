using System.Globalization;
using System.IO.Compression;
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
/// Reads score-partwise MusicXML and writes one MIDI track per part, walking every measure once in written order.
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

    public Result<byte[]> ToMidi(Stream score, bool isCompressed)
    {
        XDocument document;
        try
        {
            document = isCompressed ? LoadMxl(score) : Load(score);
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            return Result<byte[]>.Failure(ErrorCodes.MaterialScoreInvalid);
        }

        var root = document.Root!;
        if (root.Name.LocalName != "score-partwise") return Result<byte[]>.Failure(ErrorCodes.MaterialScoreUnsupported);

        try
        {
            return Convert(root);
        }
        catch (FormatException)
        {
            return Result<byte[]>.Failure(ErrorCodes.MaterialScoreInvalid);
        }
    }

    private static Result<byte[]> Convert(XElement root)
    {
        var names = root.Element("part-list")?.Elements("score-part")
            .ToDictionary(x => (string?)x.Attribute("id") ?? "", x => x.Element("part-name")?.Value.Trim());

        var tempos = new SortedDictionary<long, double>();
        var tracks = new List<TrackChunk>();
        var hasNotes = false;
        foreach (var part in root.Elements("part"))
        {
            var channel = tracks.Count % 15;
            if (channel >= PercussionChannel) channel++;
            var notes = ReadPart(part, (FourBitNumber)channel, tempos);
            hasNotes |= notes.Count > 0;

            var chunk = notes.Cast<ITimedObject>().ToTrackChunk();
            var name = names?.GetValueOrDefault((string?)part.Attribute("id") ?? "");
            chunk.Events.Insert(0, new SequenceTrackNameEvent(string.IsNullOrEmpty(name) ? $"Part {tracks.Count + 1}" : name));
            tracks.Add(chunk);
        }

        if (!hasNotes) return Result<byte[]>.Failure(ErrorCodes.MaterialScoreUnsupported);

        var file = new MidiFile(tracks) { TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarter) };
        using (var tempoManager = new TempoMapManager(file.TimeDivision))
        {
            foreach (var (tick, bpm) in tempos) tempoManager.SetTempo(tick, Tempo.FromBeatsPerMinute(bpm));
            file.ReplaceTempoMap(tempoManager.TempoMap);
        }

        using var output = new MemoryStream();
        file.Write(output);
        return Result<byte[]>.Success(output.ToArray());
    }

    /// <summary>Positions are counted in quarter notes, so a change of divisions mid-part keeps the timeline.</summary>
    private static List<Note> ReadPart(XElement part, FourBitNumber channel, SortedDictionary<long, double> tempos)
    {
        var notes = new List<Note>();
        var tied = new Dictionary<int, Note>(); // pitch → note whose tie is still open
        double divisions = 1, measureStart = 0;
        long Tick(double quarters) => (long)Math.Round(quarters * TicksPerQuarter);

        foreach (var measure in part.Elements("measure"))
        {
            double position = measureStart, measureEnd = measureStart, lastStart = measureStart;
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
                        if (element.Element("grace") is not null) break;
                        var duration = Duration(element, divisions);
                        var start = element.Element("chord") is not null ? lastStart : position;
                        if (element.Element("chord") is null) position += duration;
                        lastStart = start;
                        if (element.Element("pitch") is { } pitch && element.Element("cue") is null)
                            AddNote(notes, tied, element, Pitch(pitch), Tick(start), Tick(start + duration), channel);
                        break;
                }

                measureEnd = Math.Max(measureEnd, position);
            }

            measureStart = measureEnd;
        }

        return notes;
    }

    private static void AddNote(
        List<Note> notes, Dictionary<int, Note> tied, XElement element, int pitch, long start, long end, FourBitNumber channel)
    {
        var ties = element.Elements("tie").Select(x => (string?)x.Attribute("type")).ToList();
        if (ties.Contains("stop") && tied.TryGetValue(pitch, out var open))
        {
            open.Length = Math.Max(open.Length, end - open.Time);
        }
        else if (end > start)
        {
            open = new Note((SevenBitNumber)pitch, end - start, start) { Channel = channel, Velocity = (SevenBitNumber)Velocity };
            notes.Add(open);
        }
        else return;

        if (ties.Contains("start")) tied[pitch] = open;
        else tied.Remove(pitch);
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

using System.Text.Json;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using SoundFont = MeltySynth.SoundFont;
using Synthesizer = MeltySynth.Synthesizer;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Reads a Standard MIDI File with DryWetMidi and plays the very same timed events into MeltySynth, so the notes
/// in notes.json and the WAV share one timeline. Percussion (channel 10) is left out of both; every other channel
/// sounds as the piano of the SoundFont, whatever program the file asks for.
/// </summary>
public class MidiRenderer(IOptions<MidiRendererOptions> options) : IMidiRenderer
{
    public const int SampleRate = 22050;

    public const double MaxTimelineSeconds = 10 * 60;

    public const int MaxNotes = 100_000;

    // Lets the last notes and the pedal ring out; durationSeconds in notes.json includes it.
    private const double TailSeconds = 2;

    private const int PercussionChannel = 9;

    private const int BlockSamples = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Loaded on first render, not at startup: environments without the .sf2 (tests, a fresh clone) still start.
    // PublicationOnly does not cache a failed load, so dropping the file in later works without a restart.
    private readonly Lazy<SoundFont> _soundFont = new(
        () => new SoundFont(Path.Combine(AppContext.BaseDirectory, options.Value.SoundFontPath)),
        LazyThreadSafetyMode.PublicationOnly);

    public Result<PianoRender> Render(Stream midi)
    {
        MidiFile file;
        try
        {
            file = MidiFile.Read(midi);
        }
        catch (MidiException)
        {
            return Result<PianoRender>.Failure(ErrorCodes.MidiInvalidFile);
        }

        if (file.OriginalFormat == MidiFileFormat.MultiSequence || file.TimeDivision is not TicksPerQuarterNoteTimeDivision)
            return Result<PianoRender>.Failure(ErrorCodes.MidiFormatUnsupported);

        var tempoMap = file.GetTempoMap();
        double Seconds(long ticks) => TimeConverter.ConvertTo<MetricTimeSpan>(ticks, tempoMap).TotalMicroseconds / 1_000_000d;

        var tracks = new List<object>();
        var notes = new List<(double Start, double Duration, int Track, int Pitch, int Velocity)>();
        foreach (var chunk in file.GetTrackChunks())
        {
            var chunkNotes = chunk.GetNotes().Where(n => n.Channel != PercussionChannel).ToList();
            var noteOns = chunk.Events.OfType<NoteOnEvent>().Count(e => e.Channel != PercussionChannel && e.Velocity > 0);
            // A Note On with no Note Off would hold forever; the spec asks to reject the file instead.
            if (noteOns != chunkNotes.Count) return Result<PianoRender>.Failure(ErrorCodes.MidiInvalidFile);
            if (chunkNotes.Count == 0) continue;

            var trackId = tracks.Count;
            var name = chunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault()?.Text;
            tracks.Add(new { id = trackId, name = string.IsNullOrWhiteSpace(name) ? $"Track {trackId + 1}" : name.Trim() });

            foreach (var note in chunkNotes)
            {
                var start = Seconds(note.Time);
                var duration = Seconds(note.EndTime) - start;
                // Note On and Off on the same tick: nothing to draw, and the spec wants duration > 0.
                if (duration > 0) notes.Add((start, duration, trackId, note.NoteNumber, note.Velocity));
            }
        }

        if (notes.Count == 0) return Result<PianoRender>.Failure(ErrorCodes.MidiNoPlayableNotes);
        var notesEnd = notes.Max(n => n.Start + n.Duration);
        if (notes.Count > MaxNotes || notesEnd > MaxTimelineSeconds) return Result<PianoRender>.Failure(ErrorCodes.MidiLimitExceeded);

        var events = file.GetTimedEvents()
            .Where(e => e.Event is ChannelEvent c && c.Channel != PercussionChannel)
            .Select(e => (Sample: (int)Math.Round(Seconds(e.Time) * SampleRate), Event: (ChannelEvent)e.Event))
            .ToList();
        var durationSeconds = Math.Ceiling((notesEnd + TailSeconds) * SampleRate) / SampleRate;
        var audio = RenderWav(events, (int)(durationSeconds * SampleRate));

        notes.Sort((a, b) => (a.Start, a.Track, a.Pitch).CompareTo((b.Start, b.Track, b.Pitch)));
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            timeUnit = "seconds",
            durationSeconds,
            notesEndSeconds = Math.Round(notesEnd, 4),
            minPitch = notes.Min(n => n.Pitch),
            maxPitch = notes.Max(n => n.Pitch),
            tracks,
            notes = notes.Select(n => new
            {
                pitch = n.Pitch,
                start = Math.Round(n.Start, 4),
                duration = Math.Round(n.Duration, 4),
                velocity = n.Velocity,
                track = n.Track,
            }),
        }, JsonOptions);

        return Result<PianoRender>.Success(new PianoRender(json, audio, durationSeconds));
    }

    /// <summary>Mono 16-bit PCM WAV: no encoder delay to compensate for, unlike MP3, so seeking stays exact.</summary>
    private byte[] RenderWav(List<(int Sample, ChannelEvent Event)> events, int totalSamples)
    {
        var synthesizer = new Synthesizer(_soundFont.Value, SampleRate);
        var left = new float[BlockSamples];
        var right = new float[BlockSamples];
        var wav = new byte[44 + totalSamples * 2];
        WriteWavHeader(wav, totalSamples);

        var position = 0;
        void RenderUntil(int sample)
        {
            while (position < Math.Min(sample, totalSamples))
            {
                var count = Math.Min(BlockSamples, Math.Min(sample, totalSamples) - position);
                synthesizer.Render(left.AsSpan(0, count), right.AsSpan(0, count));
                for (var i = 0; i < count; i++)
                {
                    var value = (short)Math.Clamp((left[i] + right[i]) * 0.5f * short.MaxValue, short.MinValue, short.MaxValue);
                    BitConverter.TryWriteBytes(wav.AsSpan(44 + (position + i) * 2), value);
                }

                position += count;
            }
        }

        // GetTimedEvents is ordered by time, so each event lands at its own sample.
        foreach (var (sample, channelEvent) in events)
        {
            RenderUntil(sample);
            int channel = channelEvent.Channel;
            switch (channelEvent)
            {
                // Velocity 0 is a Note Off; MeltySynth handles it the same way.
                case NoteOnEvent on: synthesizer.ProcessMidiMessage(channel, 0x90, on.NoteNumber, on.Velocity); break;
                case NoteOffEvent off: synthesizer.ProcessMidiMessage(channel, 0x80, off.NoteNumber, off.Velocity); break;
                // Sustain pedal, volume, expression and the like; program changes are skipped to keep the piano.
                case ControlChangeEvent cc: synthesizer.ProcessMidiMessage(channel, 0xB0, cc.ControlNumber, cc.ControlValue); break;
                case PitchBendEvent bend: synthesizer.ProcessMidiMessage(channel, 0xE0, bend.PitchValue & 0x7F, bend.PitchValue >> 7); break;
            }
        }

        RenderUntil(totalSamples);
        return wav;
    }

    private static void WriteWavHeader(byte[] wav, int totalSamples)
    {
        var header = wav.AsSpan(0, 44);
        "RIFF"u8.CopyTo(header);
        BitConverter.TryWriteBytes(header[4..], 36 + totalSamples * 2);
        "WAVEfmt "u8.CopyTo(header[8..]);
        BitConverter.TryWriteBytes(header[16..], 16);             // fmt chunk size
        BitConverter.TryWriteBytes(header[20..], (short)1);       // PCM
        BitConverter.TryWriteBytes(header[22..], (short)1);       // mono
        BitConverter.TryWriteBytes(header[24..], SampleRate);
        BitConverter.TryWriteBytes(header[28..], SampleRate * 2); // byte rate
        BitConverter.TryWriteBytes(header[32..], (short)2);       // block align
        BitConverter.TryWriteBytes(header[34..], (short)16);      // bits per sample
        "data"u8.CopyTo(header[36..]);
        BitConverter.TryWriteBytes(header[40..], totalSamples * 2);
    }
}

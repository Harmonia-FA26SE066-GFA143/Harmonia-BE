using System.Runtime.InteropServices;
using System.Text.Json;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Signal-analysis scoring of a sung recording against the notes.json of a MIDI material:
/// 1. decode to mono 16 kHz (WAV in managed code, MP3/M4A through Windows Media Foundation);
/// 2. track the sung pitch with YIN, one frame every 100 ms;
/// 3. align the sung frames to each reference track with subsequence DTW, ignoring the octave so a man singing
///    a soprano line is not penalised, and keep the track that matches best;
/// 4. pitch = share of frames within half a semitone, rhythm = how steadily the alignment follows one tempo.
/// </summary>
public class PracticeAudioScorer(HttpClient httpClient, ILogger<PracticeAudioScorer> logger) : IPracticeAudioScorer
{
    public const int SampleRate = 16000;

    private const double FrameSeconds = 0.1;
    private const int Hop = (int)(SampleRate * FrameSeconds);
    // YIN integration window: two periods of the lowest note.
    private const int Window = 480;
    private const int MinLag = SampleRate / 1100;
    private const int MaxLag = SampleRate / 65;
    private const double YinThreshold = 0.15;

    private const double MaxSeconds = 10 * 60;
    // Two seconds of singing at least; below that there is nothing to judge.
    private const int MinVoicedFrames = 20;
    // Caps the DTW direction matrix at 5 min of voice x 10 min of reference (18 MB).
    private const int MaxSungFrames = 3000;

    public async Task<Result<PracticeAudioScoreResponse>> ScoreAsync(
        Stream audio, string fileName, string notesUrl, CancellationToken cancellationToken)
    {
        var notes = await DownloadAsync(notesUrl, cancellationToken);
        return notes is null
            ? Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.ExternalStorageFailed)
            : Score(audio, fileName, notes);
    }

    public async Task<Result<PracticeAudioScoreResponse>> ScoreAsync(
        string audioUrl, string fileName, string notesUrl, CancellationToken cancellationToken)
    {
        var audio = await DownloadAsync(audioUrl, cancellationToken);
        if (audio is null) return Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.ExternalStorageFailed);

        using var stream = new MemoryStream(audio);
        return await ScoreAsync(stream, fileName, notesUrl, cancellationToken);
    }

    /// <summary>The whole analysis, without network access.</summary>
    public static Result<PracticeAudioScoreResponse> Score(Stream audio, string fileName, byte[] notesJson)
    {
        var samples = Decode(audio, fileName);
        if (samples is null) return Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.PracticeAudioUnreadable);

        var sung = Voiced(TrackPitch(samples)).Take(MaxSungFrames).ToList();
        if (sung.Count < MinVoicedFrames) return Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.PracticeAudioUnreadable);

        List<(string Name, List<(int Frame, double Pitch)> Frames)> tracks;
        try
        {
            tracks = ReadReference(notesJson);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.PracticeReferenceMissing);
        }

        var best = tracks
            .Where(t => t.Frames.Count > 0)
            .Select(t => Compare(sung, t.Frames, t.Name))
            .MaxBy(x => x.Score);

        return best is null
            ? Result<PracticeAudioScoreResponse>.Failure(ErrorCodes.PracticeReferenceMissing)
            : Result<PracticeAudioScoreResponse>.Success(best);
    }

    private async Task<byte[]?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetByteArrayAsync(url, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The signed URL is not logged.
            logger.LogWarning(ex, "Practice audio scoring could not download a file");
            return null;
        }
    }

    /// <summary>Mono samples at <see cref="SampleRate"/>, at most <see cref="MaxSeconds"/>; null when undecodable.</summary>
    private static float[]? Decode(Stream audio, string fileName)
    {
        var isWav = Path.GetExtension(fileName).Equals(".wav", StringComparison.OrdinalIgnoreCase);
        // shortcut: MP3/M4A decode through Windows Media Foundation (App Service is Windows); move to FFmpeg if hosting leaves Windows.
        if (!isWav && !OperatingSystem.IsWindows()) return null;

        try
        {
            using WaveStream reader = isWav ? new WaveFileReader(audio) : new StreamMediaFoundationReader(audio);
            var channels = reader.WaveFormat.Channels;
            var provider = new WdlResamplingSampleProvider(reader.ToSampleProvider(), SampleRate);

            var mono = new List<float>();
            var buffer = new float[SampleRate * channels];
            int read;
            while (mono.Count < MaxSeconds * SampleRate && (read = provider.Read(buffer)) > 0)
            {
                for (var i = 0; i + channels <= read; i += channels)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++) sum += buffer[i + c];
                    mono.Add(sum / channels);
                }
            }

            return mono.ToArray();
        }
        catch (Exception ex) when (
            ex is FormatException or COMException or InvalidDataException or EndOfStreamException
                or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>YIN pitch per frame in MIDI semitones (69 = A4), NaN where nobody sings.</summary>
    private static double[] TrackPitch(float[] x)
    {
        // Each frame reads Window + MaxLag + 1 samples.
        var frameCount = (x.Length - Window - MaxLag - 1) / Hop + 1;
        if (frameCount <= 0) return [];

        var rms = new double[frameCount];
        for (var f = 0; f < frameCount; f++)
        {
            double sum = 0;
            for (var j = 0; j < Window; j++) sum += x[f * Hop + j] * x[f * Hop + j];
            rms[f] = Math.Sqrt(sum / Window);
        }

        // Relative to the loud part of the recording, so a quiet phone mic still counts; 95th percentile ignores clicks.
        var gate = Math.Max(1e-3, 0.1 * rms.Order().ElementAt((int)(frameCount * 0.95)));

        var pitches = new double[frameCount];
        var d = new double[MaxLag + 2];
        var cmnd = new double[MaxLag + 2];
        for (var f = 0; f < frameCount; f++)
        {
            pitches[f] = double.NaN;
            if (rms[f] < gate) continue;

            var start = f * Hop;
            double running = 0;
            for (var tau = 1; tau <= MaxLag + 1; tau++)
            {
                double sum = 0;
                for (var j = 0; j < Window; j++)
                {
                    var diff = x[start + j] - x[start + j + tau];
                    sum += diff * diff;
                }

                d[tau] = sum;
                running += sum;
                cmnd[tau] = running == 0 ? 1 : sum * tau / running;
            }

            var lag = -1;
            for (var tau = MinLag; tau <= MaxLag; tau++)
            {
                if (cmnd[tau] >= YinThreshold) continue;
                while (tau + 1 <= MaxLag && cmnd[tau + 1] < cmnd[tau]) tau++;
                lag = tau;
                break;
            }

            if (lag < 0) continue;

            // Parabolic interpolation: at 16 kHz a soprano's period is only ~16 samples.
            var (a, b, c) = (cmnd[lag - 1], cmnd[lag], cmnd[lag + 1]);
            var denominator = a - 2 * b + c;
            var refined = lag + (denominator == 0 ? 0 : 0.5 * (a - c) / denominator);
            pitches[f] = 69 + 12 * Math.Log2(SampleRate / refined / 440);
        }

        return pitches;
    }

    private static IEnumerable<(int Frame, double Pitch)> Voiced(double[] pitches) =>
        pitches.Select((p, i) => (Frame: i, Pitch: p)).Where(x => !double.IsNaN(x.Pitch));

    /// <summary>Per track of notes.json (schema v1): the highest note sounding at each frame centre.</summary>
    private static List<(string Name, List<(int Frame, double Pitch)> Frames)> ReadReference(byte[] notesJson)
    {
        using var document = JsonDocument.Parse(notesJson);
        var root = document.RootElement;
        var notes = root.GetProperty("notes").EnumerateArray()
            .Select(n => (
                Track: n.GetProperty("track").GetInt32(),
                Pitch: n.GetProperty("pitch").GetInt32(),
                Start: n.GetProperty("start").GetDouble(),
                End: n.GetProperty("start").GetDouble() + n.GetProperty("duration").GetDouble()))
            .ToList();

        return root.GetProperty("tracks").EnumerateArray()
            .Select(t => (Id: t.GetProperty("id").GetInt32(), Name: t.GetProperty("name").GetString() ?? string.Empty))
            .Select(t =>
            {
                var trackNotes = notes.Where(n => n.Track == t.Id).ToList();
                var end = trackNotes.Count == 0 ? 0 : trackNotes.Max(n => n.End);
                var pitches = new double[(int)Math.Ceiling(end / FrameSeconds)];
                for (var f = 0; f < pitches.Length; f++)
                {
                    var time = (f + 0.5) * FrameSeconds;
                    pitches[f] = trackNotes.Where(n => n.Start <= time && time < n.End)
                        .Select(n => (double)n.Pitch).DefaultIfEmpty(double.NaN).Max();
                }

                return (t.Name, Voiced(pitches).ToList());
            })
            .ToList();
    }

    /// <summary>Distance in semitones ignoring the octave, 0..6.</summary>
    private static double PitchDistance(double sung, double reference)
    {
        var d = Math.Abs(sung - reference) % 12;
        return Math.Min(d, 12 - d);
    }

    private static PracticeAudioScoreResponse Compare(
        List<(int Frame, double Pitch)> sung, List<(int Frame, double Pitch)> reference, string trackName)
    {
        int n = sung.Count, m = reference.Count;
        // Subsequence DTW: the recording may cover only part of the reference, so it may start and end anywhere in it.
        var direction = new byte[n * m];
        var previous = new double[m];
        var current = new double[m];
        for (var j = 0; j < m; j++) previous[j] = PitchDistance(sung[0].Pitch, reference[j].Pitch);

        for (var i = 1; i < n; i++)
        {
            current[0] = previous[0] + PitchDistance(sung[i].Pitch, reference[0].Pitch);
            direction[i * m] = 1;
            for (var j = 1; j < m; j++)
            {
                var cost = PitchDistance(sung[i].Pitch, reference[j].Pitch);
                var (best, step) = (previous[j - 1], (byte)0);
                if (previous[j] < best) (best, step) = (previous[j], (byte)1);
                if (current[j - 1] < best) (best, step) = (current[j - 1], (byte)2);
                current[j] = best + cost;
                direction[i * m + j] = step;
            }

            (previous, current) = (current, previous);
        }

        // Walk back from the cheapest end. A sung frame matched to several reference frames keeps its closest
        // pitch and its earliest reference frame.
        var end = Array.IndexOf(previous, previous.Min());
        var matches = new (int I, int J, double Distance)[n];
        var bestDistance = Enumerable.Repeat(double.MaxValue, n).ToArray();
        for (int i = n - 1, j = end; ;)
        {
            var distance = PitchDistance(sung[i].Pitch, reference[j].Pitch);
            if (distance < bestDistance[i]) bestDistance[i] = distance;
            matches[i] = (i, j, bestDistance[i]);
            if (i == 0) break;
            switch (direction[i * m + j])
            {
                case 0: i--; j--; break;
                case 1: i--; break;
                default: j--; break;
            }
        }

        var pitch = matches.Average(x => x.Distance <= 0.5 ? 1 : x.Distance <= 1 ? 0.5 : 0);

        // Rhythm: fit sung time = a * reference time + b; a steady singer stays close to that line.
        var sungTimes = matches.Select(x => sung[x.I].Frame * FrameSeconds).ToArray();
        var refTimes = matches.Select(x => reference[x.J].Frame * FrameSeconds).ToArray();
        var (meanRef, meanSung) = (refTimes.Average(), sungTimes.Average());
        var variance = refTimes.Sum(t => (t - meanRef) * (t - meanRef));
        double rhythm = 0;
        if (variance > 0)
        {
            var slope = refTimes.Zip(sungTimes).Sum(p => (p.First - meanRef) * (p.Second - meanSung)) / variance;
            var intercept = meanSung - slope * meanRef;
            var residual = Math.Sqrt(refTimes.Zip(sungTimes).Average(p => Math.Pow(p.Second - (slope * p.First + intercept), 2)));
            // Half a second of drift scores zero; a tempo within 15% of the reference is not penalised.
            var steadiness = Math.Clamp(1 - residual / 0.5, 0, 1);
            var tempo = Math.Clamp(1 - Math.Max(0, Math.Abs(slope - 1) - 0.15) * 2, 0, 1);
            rhythm = steadiness * tempo;
        }

        var span = reference[^1].Frame - reference[0].Frame + 1;
        var covered = reference[matches[^1].J].Frame - reference[matches[0].J].Frame + 1;
        var coverage = Math.Clamp((double)covered / span, 0, 1);

        return new PracticeAudioScoreResponse(
            Score: (int)Math.Round(100 * (0.7 * pitch + 0.3 * rhythm)),
            PitchScore: (int)Math.Round(100 * pitch),
            RhythmScore: (int)Math.Round(100 * rhythm),
            CoveragePercent: (int)Math.Round(100 * coverage),
            TrackName: trackName);
    }
}

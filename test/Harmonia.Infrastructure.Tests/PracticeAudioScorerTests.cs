using System.Text.Json;
using Harmonia.Domain.Common;
using Harmonia.Infrastructure.ExternalServices;
using NAudio.Wave;

namespace Harmonia.Infrastructure.Tests;

/// <summary>Sings sine-wave melodies into a WAV (the decoder that runs without Media Foundation) and scores them.</summary>
public class PracticeAudioScorerTests
{
    // C major arpeggio, half a second per note: moved two semitones it shares no pitch class with itself.
    private static readonly int[] Melody = [60, 64, 67, 72, 67, 64, 60, 64];
    private const double NoteSeconds = 0.5;

    private static MemoryStream Sing(IEnumerable<int> pitches, int transpose = 0)
    {
        const int rate = 22050;
        var samples = new List<float>();
        foreach (var pitch in pitches)
        {
            var frequency = 440 * Math.Pow(2, (pitch + transpose - 69) / 12.0);
            for (var i = 0; i < rate * NoteSeconds; i++) samples.Add((float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / rate)));
        }

        return Wav(samples.ToArray(), rate);
    }

    private static MemoryStream Wav(float[] samples, int rate)
    {
        var stream = new MemoryStream();
        using (var writer = new WaveFileWriter(stream, WaveFormat.CreateIeeeFloatWaveFormat(rate, 1)))
            writer.WriteSamples(samples, 0, samples.Length);
        stream.Position = 0;
        return stream;
    }

    /// <summary>notes.json schema v1, one track per melody.</summary>
    private static byte[] Notes(params (string Name, int[] Pitches)[] tracks) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            tracks = tracks.Select((t, i) => new { id = i, name = t.Name }),
            notes = tracks.SelectMany((t, i) => t.Pitches.Select((p, n) =>
                new { pitch = p, start = n * NoteSeconds, duration = NoteSeconds, velocity = 90, track = i })),
        });

    [Fact]
    public void Score_ExactMelody_ScoresHighAndPicksMatchingTrack()
    {
        int[] bass = [41, 43, 44, 46, 41, 43, 44, 46];

        var result = PracticeAudioScorer.Score(Sing(Melody), "take.wav", Notes(("Bass", bass), ("Soprano", Melody)));

        Assert.True(result.IsSuccess, result.Code);
        Assert.Equal("Soprano", result.Value!.TrackName);
        Assert.True(result.Value.PitchScore >= 90, $"pitch {result.Value.PitchScore}");
        Assert.True(result.Value.Score >= 85, $"score {result.Value.Score}");
        Assert.True(result.Value.CoveragePercent >= 90, $"coverage {result.Value.CoveragePercent}");
    }

    [Fact]
    public void Score_OctaveLower_IsNotPenalised()
    {
        var result = PracticeAudioScorer.Score(Sing(Melody, transpose: -12), "take.wav", Notes(("Soprano", Melody)));

        Assert.True(result.Value!.PitchScore >= 90, $"pitch {result.Value.PitchScore}");
    }

    [Fact]
    public void Score_TwoSemitonesSharp_ScoresLowPitch()
    {
        var result = PracticeAudioScorer.Score(Sing(Melody, transpose: 2), "take.wav", Notes(("Soprano", Melody)));

        Assert.True(result.Value!.PitchScore <= 30, $"pitch {result.Value.PitchScore}");
    }

    [Fact]
    public void Score_Silence_ReturnsAudioUnreadable()
    {
        var result = PracticeAudioScorer.Score(Wav(new float[22050 * 4], 22050), "take.wav", Notes(("Soprano", Melody)));

        Assert.Equal(ErrorCodes.PracticeAudioUnreadable, result.Code);
    }

    [Fact]
    public void Score_NotAudio_ReturnsAudioUnreadable()
    {
        var result = PracticeAudioScorer.Score(new MemoryStream([1, 2, 3]), "take.wav", Notes(("Soprano", Melody)));

        Assert.Equal(ErrorCodes.PracticeAudioUnreadable, result.Code);
    }
}

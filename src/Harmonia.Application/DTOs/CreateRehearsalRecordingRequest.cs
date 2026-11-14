namespace Harmonia.Application.DTOs;

/// <summary>Form fields of the multipart upload; the audio itself travels separately as <see cref="Common.Models.FileContent"/>.</summary>
public class CreateRehearsalRecordingRequest
{
    /// <summary>The song being practised; leave empty for a take of the whole session.</summary>
    public Guid? SongId { get; set; }

    public string? Title { get; set; }

    /// <summary>Reported by the recorder, display only; the server does not measure the audio.</summary>
    public int? DurationSeconds { get; set; }
}

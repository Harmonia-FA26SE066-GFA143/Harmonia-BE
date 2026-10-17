namespace Harmonia.Application.DTOs;

/// <summary>Form fields of the multipart upload; the audio itself travels separately as <see cref="Common.Models.FileContent"/>.</summary>
public class CreatePracticeSubmissionRequest
{
    /// <summary>Reported by the recording app, display only; the server does not measure the audio.</summary>
    public int? DurationSeconds { get; set; }
}

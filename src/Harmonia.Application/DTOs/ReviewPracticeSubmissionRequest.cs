using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class ReviewPracticeSubmissionRequest
{
    /// <summary>Passed or NeedsRevision only.</summary>
    public SubmissionStatus Result { get; set; }

    /// <summary>Required for NeedsRevision, so the member knows what to fix.</summary>
    public string? Comment { get; set; }
}

using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class PracticeSubmissionDto
{
    public Guid Id { get; set; }

    public Guid PracticeAssignmentId { get; set; }

    public int AttemptNo { get; set; }

    public DateTime SubmittedAt { get; set; }

    public SubmissionStatus Status { get; set; }

    public int? DurationSeconds { get; set; }

    /// <summary>Short-lived signed URL of the private audio file.</summary>
    public string AudioUrl { get; set; } = string.Empty;
}

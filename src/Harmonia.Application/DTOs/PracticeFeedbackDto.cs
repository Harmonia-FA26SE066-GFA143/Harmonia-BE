using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class PracticeFeedbackDto
{
    public Guid Id { get; set; }

    /// <summary>The result this review set; the newest feedback always matches the submission's status.</summary>
    public SubmissionStatus Result { get; set; }

    public string? Comment { get; set; }

    public Guid ReviewerId { get; set; }

    public string? ReviewerName { get; set; }

    public DateTime ReviewedAt { get; set; }
}

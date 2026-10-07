using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class PracticeFeedbackDto
{
    public Guid Id { get; set; }

    public SubmissionStatus Result { get; set; }

    public string? Comment { get; set; }

    public Guid ReviewerId { get; set; }

    public DateTime ReviewedAt { get; set; }
}

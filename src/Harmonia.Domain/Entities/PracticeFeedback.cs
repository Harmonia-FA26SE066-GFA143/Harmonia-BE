using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class PracticeFeedback : BaseEntity
{
    public Guid SubmissionId { get; set; }

    public Guid ReviewerId { get; set; }

    // Only Passed or NeedsRevision.
    public SubmissionStatus Result { get; set; }

    public string? Comment { get; set; }

    public DateTime ReviewedAt { get; set; }

    public PracticeSubmission Submission { get; set; } = null!;

    public User Reviewer { get; set; } = null!;
}

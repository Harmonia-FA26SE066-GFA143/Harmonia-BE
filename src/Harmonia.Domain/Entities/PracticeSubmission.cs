using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class PracticeSubmission : BaseEntity
{
    public Guid PracticeAssignmentId { get; set; }

    public Guid MemberId { get; set; }

    public string AudioUrl { get; set; } = string.Empty;

    public int? DurationSeconds { get; set; }

    public int AttemptNo { get; set; }

    public DateTime SubmittedAt { get; set; }

    public SubmissionStatus Status { get; set; }

    public PracticeAssignment PracticeAssignment { get; set; } = null!;

    public MemberProfile Member { get; set; } = null!;

    public ICollection<PracticeFeedback> Feedbacks { get; set; } = [];
}

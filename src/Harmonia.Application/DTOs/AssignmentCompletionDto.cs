namespace Harmonia.Application.DTOs;

/// <summary>
/// One active member's practice assignments due inside the report's range, split by the state of their newest
/// attempt (UC-34 / FE-52). The four state counts add up to <see cref="AssignedCount"/>.
/// </summary>
public class AssignmentCompletionDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public int AssignedCount { get; set; }

    public int PassedCount { get; set; }

    public int NeedsRevisionCount { get; set; }

    /// <summary>Submitted and not reviewed yet.</summary>
    public int AwaitingReviewCount { get; set; }

    public int NotSubmittedCount { get; set; }
}

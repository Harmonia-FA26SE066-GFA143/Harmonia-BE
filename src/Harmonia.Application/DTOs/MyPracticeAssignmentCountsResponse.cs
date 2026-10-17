namespace Harmonia.Application.DTOs;

/// <summary>
/// How the calling member's assignments stand. NotSubmitted, Submitted, NeedsRevision and Passed split Total by the
/// newest attempt; Overdue overlaps them (an overdue assignment is also not submitted, submitted or needs revision).
/// </summary>
public record MyPracticeAssignmentCountsResponse(
    int Total,
    int NotSubmitted,
    int Submitted,
    int NeedsRevision,
    int Passed,
    int Overdue);

namespace Harmonia.Application.DTOs;

/// <summary>
/// Rehearsal attendance and practice completion of the reported members in one period. Attendance counts rehearsals
/// starting in the period; practice counts assignments due in it, judged by each member's newest attempt, the same
/// rules as the per-member reports.
/// </summary>
public class ActivityTrendDto
{
    /// <summary>First day of the period, clipped to the requested range.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Last day of the period, clipped to the requested range.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Rehearsals with at least one recorded attendance of the reported members.</summary>
    public int RehearsalsRecorded { get; set; }

    public int PresentCount { get; set; }

    public int LateCount { get; set; }

    public int AbsentCount { get; set; }

    public int ExcusedCount { get; set; }

    /// <summary>(Present + Late) / every recorded attendance, 0–1; null when nothing was recorded.</summary>
    public double? AttendanceRate { get; set; }

    /// <summary>Assignments due in the period that reach at least one reported member.</summary>
    public int AssignmentsDue { get; set; }

    /// <summary>Member–assignment pairs, the base of the counts below.</summary>
    public int AssignedCount { get; set; }

    public int PassedCount { get; set; }

    public int NeedsRevisionCount { get; set; }

    public int AwaitingReviewCount { get; set; }

    public int NotSubmittedCount { get; set; }

    /// <summary>Passed / assigned, 0–1; null when nothing was assigned.</summary>
    public double? CompletionRate { get; set; }
}

namespace Harmonia.Application.DTOs;

/// <summary>
/// A member's track record against the nomination thresholds of the system settings. A threshold that is not met
/// is a warning only: the Parish Priest still decides.
/// </summary>
public class DirectorNominationStatsDto
{
    /// <summary>Joined date of the member's first choir; null when they never joined one.</summary>
    public DateOnly? FirstJoinedDate { get; set; }

    public int TenureMonths { get; set; }

    public int MinTenureMonths { get; set; }

    public bool MeetsTenure { get; set; }

    /// <summary>Rehearsal attendances recorded in the last 12 months.</summary>
    public int AttendanceRecorded { get; set; }

    /// <summary>Of <see cref="AttendanceRecorded"/>, those Present or Late.</summary>
    public int AttendancePresent { get; set; }

    /// <summary>0–100, rounded down; null when nothing was recorded.</summary>
    public int? AttendancePercent { get; set; }

    public int MinAttendancePercent { get; set; }

    public bool MeetsAttendance { get; set; }

    public int ServedEventCount { get; set; }

    public int MinServedEvents { get; set; }

    public bool MeetsServedEvents { get; set; }

    public bool HasConductingSkill { get; set; }

    public bool IsConductingSkillRequired { get; set; }

    public bool MeetsConductingSkill { get; set; }

    public bool MeetsAll { get; set; }
}

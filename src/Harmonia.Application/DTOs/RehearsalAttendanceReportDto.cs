namespace Harmonia.Application.DTOs;

/// <summary>One active member's recorded attendance over the rehearsals of the report's range (UC-34 / FE-52).</summary>
public class RehearsalAttendanceReportDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public int PresentCount { get; set; }

    public int LateCount { get; set; }

    public int AbsentCount { get; set; }

    public int ExcusedCount { get; set; }
}

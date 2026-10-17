using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>A rehearsal as the calling user sees it, with their own attendance.</summary>
public class MemberRehearsalDetailDto : RehearsalSummaryDto
{
    /// <summary>Null until the director records the caller, and always null for a caller who is not a member.</summary>
    public AttendanceStatus? MyAttendanceStatus { get; set; }
}

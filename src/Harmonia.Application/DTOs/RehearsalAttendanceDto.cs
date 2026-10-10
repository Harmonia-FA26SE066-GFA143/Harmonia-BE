using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One line of the attendance sheet; <see cref="Status"/> is null until the member is recorded.</summary>
public class RehearsalAttendanceDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public AttendanceStatus? Status { get; set; }

    public DateTime? CheckedAt { get; set; }
}

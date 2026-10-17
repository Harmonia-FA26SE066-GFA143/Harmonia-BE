using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class RecordRehearsalAttendanceRequest
{
    public Guid MemberId { get; set; }

    public AttendanceStatus Status { get; set; }
}

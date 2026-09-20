using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class RehearsalAttendance : BaseEntity
{
    public Guid RehearsalId { get; set; }

    public Guid MemberId { get; set; }

    public AttendanceStatus Status { get; set; }

    public Guid CheckedBy { get; set; }

    public DateTime CheckedAt { get; set; }

    public Rehearsal Rehearsal { get; set; } = null!;

    public MemberProfile Member { get; set; } = null!;

    public User Checker { get; set; } = null!;
}

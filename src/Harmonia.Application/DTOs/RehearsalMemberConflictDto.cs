namespace Harmonia.Application.DTOs;

/// <summary>A member assigned to serve an event whose time clashes with a rehearsal of their choir.</summary>
public class RehearsalMemberConflictDto
{
    public Guid MemberId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public DateOnly EventDate { get; set; }

    public TimeOnly Time { get; set; }
}

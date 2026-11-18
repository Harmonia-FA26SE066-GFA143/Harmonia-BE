namespace Harmonia.Application.DTOs;

public class CreateRosterAssignmentRequest
{
    public Guid EventId { get; set; }

    /// <summary>Null for a whole-service line, filling one of the event's own personnel requirements.</summary>
    public Guid? SongListItemId { get; set; }

    public Guid SkillId { get; set; }

    public Guid MemberId { get; set; }

    public string? Note { get; set; }
}

namespace Harmonia.Application.DTOs;

public class CreateRosterAssignmentRequest
{
    public Guid EventId { get; set; }

    public Guid SongListItemId { get; set; }

    public Guid SkillId { get; set; }

    public Guid MemberId { get; set; }
}

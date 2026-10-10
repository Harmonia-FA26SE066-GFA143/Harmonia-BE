using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class RosterAssignmentDto
{
    public Guid Id { get; set; }

    public Guid MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public Guid? SongListItemId { get; set; }

    public string? SongTitle { get; set; }

    public AssignmentSource Source { get; set; }
}

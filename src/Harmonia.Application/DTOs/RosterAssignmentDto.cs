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

    public string? Note { get; set; }

    public AssignmentSource Source { get; set; }

    /// <summary>The member's choir rehearses at a time that clashes with this event; a warning only.</summary>
    public bool HasRehearsalConflict { get; set; }
}

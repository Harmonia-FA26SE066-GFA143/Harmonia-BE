using System;

namespace Harmonia.Application.DTOs;

/// <summary>One line of a finalized, notified service roster that belongs to the calling member.</summary>
public class MyAssignmentDto
{
    public Guid SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;

    public Guid? SongListItemId { get; set; }

    public string? SongTitle { get; set; }
}

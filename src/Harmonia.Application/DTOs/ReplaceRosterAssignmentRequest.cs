namespace Harmonia.Application.DTOs;

public class ReplaceRosterAssignmentRequest
{
    /// <summary>The member who takes over the replaced assignment's song and skill.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Null keeps the replaced line's note, so the role stays described.</summary>
    public string? Note { get; set; }
}

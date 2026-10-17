namespace Harmonia.Application.DTOs;

public class ReplaceRosterAssignmentRequest
{
    /// <summary>The member who takes over the replaced assignment's song and skill.</summary>
    public Guid MemberId { get; set; }
}

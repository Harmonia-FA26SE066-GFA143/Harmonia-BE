namespace Harmonia.Application.DTOs;

/// <summary>A submission as the Choir Director reviews it: who sent it and for which assignment.</summary>
public class PracticeSubmissionDetailDto : PracticeSubmissionDto
{
    public Guid MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public string? MemberAvatarUrl { get; set; }

    public string AssignmentTitle { get; set; } = string.Empty;

    public DateTime AssignmentDueDate { get; set; }
}

namespace Harmonia.Application.DTOs;

/// <summary>A submission with who sent it, for which assignment, and every review so far.</summary>
public class PracticeSubmissionDetailDto : PracticeSubmissionDto
{
    /// <summary>Oldest first; the last one carries the current result.</summary>
    public List<PracticeFeedbackDto> Feedbacks { get; set; } = [];

    public Guid MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public string? MemberAvatarUrl { get; set; }

    public string AssignmentTitle { get; set; } = string.Empty;

    public DateTime AssignmentDueDate { get; set; }
}

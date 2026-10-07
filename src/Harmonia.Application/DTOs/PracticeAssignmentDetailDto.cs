using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>An assignment as the receiving member sees it, with the member's own newest submission.</summary>
public class PracticeAssignmentDetailDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    public DateTime DueDate { get; set; }

    public Guid? EventId { get; set; }

    public DateOnly? EventDate { get; set; }

    public TimeOnly? EventTime { get; set; }

    public string? EventTitle { get; set; }

    public Guid? SongId { get; set; }

    public string? SongTitle { get; set; }

    public Guid? MaterialId { get; set; }

    public string? MaterialTitle { get; set; }

    public MaterialType? MaterialType { get; set; }

    /// <summary>Null when the member has not submitted yet.</summary>
    public SubmissionStatus? LatestSubmissionStatus { get; set; }

    public DateTime? LatestSubmittedAt { get; set; }
}

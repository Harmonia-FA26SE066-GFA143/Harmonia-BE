namespace Harmonia.Application.DTOs;

/// <summary>A reviewed submission together with the review just recorded.</summary>
public class PracticeSubmissionReviewDto : PracticeSubmissionDetailDto
{
    public PracticeFeedbackDto Feedback { get; set; } = null!;
}

using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class AddPracticeFeedbackRequest
{
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Null keeps the current result. Passed or NeedsRevision changes it, allowed only on the member's newest attempt.
    /// </summary>
    public SubmissionStatus? Result { get; set; }
}

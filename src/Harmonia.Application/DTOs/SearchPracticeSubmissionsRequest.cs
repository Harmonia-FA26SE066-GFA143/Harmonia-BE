using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/practice-submissions and GET api/practice-assignments/{id}/submissions.</summary>
public class SearchPracticeSubmissionsRequest : PagingRequest
{
    public SubmissionStatus? Status { get; set; }

    /// <summary>False (default): only each member's newest attempt. True: every attempt.</summary>
    public bool AllAttempts { get; set; }
}

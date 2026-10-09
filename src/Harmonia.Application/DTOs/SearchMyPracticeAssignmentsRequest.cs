using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/practice-assignments/mine. Filters combine with AND.</summary>
public class SearchMyPracticeAssignmentsRequest : PagingRequest
{
    /// <summary>True: due date not passed yet. False: due date passed. Null: both.</summary>
    public bool? IsOpen { get; set; }

    /// <summary>
    /// Submitted, Passed or NeedsRevision: the member's newest attempt has that status.
    /// Overdue: the assignment is overdue for the member (see <c>PracticeAssignment.IsOverdue</c>).
    /// </summary>
    public SubmissionStatus? Status { get; set; }

    /// <summary>False: the member has never submitted. True: at least one attempt. Null: both.</summary>
    public bool? HasSubmission { get; set; }
}

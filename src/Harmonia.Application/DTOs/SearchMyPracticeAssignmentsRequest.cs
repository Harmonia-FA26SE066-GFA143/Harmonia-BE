using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/practice-assignments/mine.</summary>
public class SearchMyPracticeAssignmentsRequest : PagingRequest
{
    /// <summary>True: due date not passed yet. False: due date passed. Null: both.</summary>
    public bool? IsOpen { get; set; }
}

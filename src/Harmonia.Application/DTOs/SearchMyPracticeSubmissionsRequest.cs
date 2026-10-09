using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/practice-submissions/mine.</summary>
public class SearchMyPracticeSubmissionsRequest : PagingRequest
{
    public Guid? AssignmentId { get; set; }
}

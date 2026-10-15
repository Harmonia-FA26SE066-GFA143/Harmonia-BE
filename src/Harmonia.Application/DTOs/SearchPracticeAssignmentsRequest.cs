using Harmonia.Application.Common.Models;
using System;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/practice-assignments. No event: every assignment.</summary>
public class SearchPracticeAssignmentsRequest : PagingRequest
{
    public Guid? EventId { get; set; }
}

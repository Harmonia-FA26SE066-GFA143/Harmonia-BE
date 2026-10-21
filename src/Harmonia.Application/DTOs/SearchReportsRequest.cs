using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of the GET api/reports endpoints. Every filter is optional and they combine with AND.</summary>
public class SearchReportsRequest : PagingRequest
{
    public Guid? LiturgicalSeasonId { get; set; }

    /// <summary>Narrows the report to one event. The user activity report ignores it.</summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Keeps only the members active in this choir; their activity is still counted over every event.
    /// Only the three per-member reports use it.
    /// </summary>
    public Guid? ChoirId { get; set; }

    /// <summary>Inclusive.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Inclusive.</summary>
    public DateOnly? ToDate { get; set; }
}

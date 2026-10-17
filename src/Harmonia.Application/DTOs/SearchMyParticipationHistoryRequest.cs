using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/member-profiles/me/history. Every filter is optional and they combine with AND.</summary>
public class SearchMyParticipationHistoryRequest : PagingRequest
{
    public Guid? LiturgicalSeasonId { get; set; }

    /// <summary>Inclusive.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Inclusive.</summary>
    public DateOnly? ToDate { get; set; }
}

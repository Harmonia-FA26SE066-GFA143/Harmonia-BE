using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/audit-logs. Every filter is optional and they combine with AND.</summary>
public class SearchAuditLogsRequest : PagingRequest
{
    /// <summary>The user who made the change.</summary>
    public Guid? UserId { get; set; }

    public string? EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string? Action { get; set; }

    /// <summary>Inclusive, Vietnam date.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Inclusive, Vietnam date.</summary>
    public DateOnly? ToDate { get; set; }
}

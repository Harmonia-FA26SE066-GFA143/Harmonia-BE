using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;
using System;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/liturgical-events: paging plus optional date range and status filters (AND).</summary>
public class SearchLiturgicalEventsRequest : PagingRequest
{
    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public EventStatus? Status { get; set; }
}

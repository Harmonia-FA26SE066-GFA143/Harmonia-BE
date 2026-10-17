using Harmonia.Application.Common.Models;
using System;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/rehearsals. Filters combine with AND; the dates apply to the start time.</summary>
public class SearchRehearsalsRequest : PagingRequest
{
    public Guid? EventId { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }
}

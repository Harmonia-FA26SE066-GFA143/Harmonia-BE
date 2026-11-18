using System;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/liturgical-days. Both dates are inclusive.</summary>
public class SearchLiturgicalDaysRequest
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }
}

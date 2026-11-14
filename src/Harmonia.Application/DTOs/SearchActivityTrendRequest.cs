using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/reports/activity-trend.</summary>
public class SearchActivityTrendRequest
{
    public ReportPeriod GroupBy { get; set; } = ReportPeriod.Month;

    /// <summary>Required, inclusive.</summary>
    public DateOnly? FromDate { get; set; }

    /// <summary>Required, inclusive, at most <c>MaxDays</c> of the validator after <see cref="FromDate"/>.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>Keeps only the members active in this choir, as in the per-member reports.</summary>
    public Guid? ChoirId { get; set; }
}

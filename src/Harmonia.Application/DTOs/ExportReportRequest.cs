using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/reports/export. The filters are those of the report being viewed; the paging
/// values are ignored because an export holds every matching row.
/// </summary>
public class ExportReportRequest : SearchReportsRequest
{
    public ReportType? Type { get; set; }

    public ReportFormat Format { get; set; }
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Exporting the reports of <see cref="IReportService"/> as files, for the Admin (UC-34E / FE-53).</summary>
public interface IReportExportService
{
    /// <summary>
    /// Builds a file holding every row the report has for the filters, stores it privately and records the
    /// export. REPORT_NO_DATA when the report is empty; EXTERNAL_STORAGE_FAILED when the file could not be stored.
    /// </summary>
    Task<Result<FileContent>> ExportAsync(
        Guid adminUserId, ExportReportRequest request, CancellationToken cancellationToken);

    /// <summary>Every Admin's past exports, newest first.</summary>
    Task<Result<PagedList<ReportExportDto>>> GetExportsAsync(PagingRequest request, CancellationToken cancellationToken);
}

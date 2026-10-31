using Harmonia.Application.Common.Models;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IReportExportRepository : IGenericRepository<ReportExport>
{
    /// <summary>Read-only page of every Admin's exports, newest first, with <see cref="ReportExport.Exporter"/> loaded.</summary>
    Task<PagedList<ReportExport>> GetPageAsync(PagingRequest paging, CancellationToken cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class ReportExportRepository(HarmoniaDbContext dbContext)
    : GenericRepository<ReportExport>(dbContext), IReportExportRepository
{
    public async Task<PagedList<ReportExport>> GetPageAsync(PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.ReportExports.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Exporter)
            .OrderByDescending(x => x.ExportedAt)
            .ThenBy(x => x.Id)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<ReportExport>(items, paging.PageNumber, paging.PageSize, totalCount);
    }
}

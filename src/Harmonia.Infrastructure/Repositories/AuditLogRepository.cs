using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class AuditLogRepository(HarmoniaDbContext dbContext)
    : GenericRepository<AuditLog>(dbContext), IAuditLogRepository
{
    public async Task<PagedList<AuditLog>> SearchAsync(SearchAuditLogsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.AuditLogs.AsNoTracking();

        if (filter.UserId is { } userId)
        {
            query = query.Where(x => x.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(x => x.EntityType == filter.EntityType);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(x => x.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(x => x.Action == filter.Action);
        }

        if (filter.FromDate is { } fromDate)
        {
            var fromUtc = VietnamTime.StartOfDayUtc(fromDate);
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }

        if (filter.ToDate is { } toDate)
        {
            var toUtc = VietnamTime.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(x => x.CreatedAt < toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<AuditLog>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}

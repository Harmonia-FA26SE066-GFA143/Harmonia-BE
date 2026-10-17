using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    /// <summary>
    /// Read-only page of entries matching <paramref name="filter"/>, newest first, with
    /// <see cref="AuditLog.User"/> loaded. The date range is in Vietnam dates, both ends included.
    /// </summary>
    Task<PagedList<AuditLog>> SearchAsync(SearchAuditLogsRequest filter, CancellationToken cancellationToken);
}

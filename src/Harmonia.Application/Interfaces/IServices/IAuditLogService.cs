using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>The activity history, for the Admin (UC-35 / FE-54).</summary>
public interface IAuditLogService
{
    /// <summary>Entries matching the filters, newest first.</summary>
    Task<Result<PagedList<AuditLogDto>>> SearchAsync(SearchAuditLogsRequest request, CancellationToken cancellationToken);
}

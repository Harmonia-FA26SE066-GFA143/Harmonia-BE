using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;

namespace Harmonia.Application.Services;

public class AuditLogService(IAuditLogRepository auditLogRepository, IMapper mapper) : IAuditLogService
{
    public async Task<Result<PagedList<AuditLogDto>>> SearchAsync(
        SearchAuditLogsRequest request, CancellationToken cancellationToken)
    {
        var page = await auditLogRepository.SearchAsync(request, cancellationToken);

        return Result<PagedList<AuditLogDto>>.Success(new PagedList<AuditLogDto>(
            mapper.Map<List<AuditLogDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }
}

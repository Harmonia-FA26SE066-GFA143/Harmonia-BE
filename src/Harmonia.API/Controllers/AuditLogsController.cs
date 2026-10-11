using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Harmonia.API.Controllers;

/// <summary>The history of important activities, for the Admin (UC-35 / FE-54).</summary>
[Route("api/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public class AuditLogsController(IAuditLogService auditLogService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] SearchAuditLogsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await auditLogService.SearchAsync(request, cancellationToken));
}

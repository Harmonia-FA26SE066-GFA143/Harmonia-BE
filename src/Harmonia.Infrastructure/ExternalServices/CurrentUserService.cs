using System.Security.Claims;
using Harmonia.Application.Interfaces.IServices;
using Microsoft.AspNetCore.Http;

namespace Harmonia.Infrastructure.ExternalServices;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? RoleName => User?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

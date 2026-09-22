using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
}

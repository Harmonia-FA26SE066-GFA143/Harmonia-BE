using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Verifies the current password, sets the new one and signs out every device.</summary>
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Always succeeds so callers cannot tell which emails are registered.</summary>
    Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Consumes the emailed token, sets the new password and signs out every device.</summary>
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}

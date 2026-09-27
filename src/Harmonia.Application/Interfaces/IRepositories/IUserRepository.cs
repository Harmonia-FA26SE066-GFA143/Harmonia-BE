using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="PasswordResetToken.User"/> loaded.</summary>
    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken);

    /// <summary>Marks the user's unused reset tokens for deletion, so only the newest emailed link works.</summary>
    Task RemoveUnusedPasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken);
}

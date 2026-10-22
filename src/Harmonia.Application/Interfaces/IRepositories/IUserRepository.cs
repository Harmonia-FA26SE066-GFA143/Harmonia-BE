using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// One page of users with their Role, ordered by email. <paramref name="keyword"/> matches part of
    /// the email; the role and active filters of <paramref name="filter"/> narrow the result when set.
    /// </summary>
    Task<PagedList<User>> SearchAsync(string? keyword, SearchUsersRequest filter, CancellationToken cancellationToken);

    /// <summary>
    /// One page of every user with their Role, ordered by email, with audit log counts inside the date range
    /// of <paramref name="filter"/> (its season is ignored): password changes made while signed in, password
    /// resets through the emailed link, edits of the account itself, and all entries the user caused.
    /// </summary>
    Task<PagedList<(User User, int PasswordChangeCount, int PasswordResetCount, int AccountChangeCount, int ActionCount)>>
        GetActivityAsync(SearchReportsRequest filter, CancellationToken cancellationToken);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="PasswordResetToken.User"/> loaded.</summary>
    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken);

    /// <summary>Marks the user's unused reset tokens for deletion, so only the newest emailed link works.</summary>
    Task RemoveUnusedPasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken);

    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<List<Guid>> GetActiveUserIdsByRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken);

    /// <summary>Read-only active users of the role, with Role loaded, ordered by name.</summary>
    Task<List<User>> GetActiveByRoleAsync(string roleName, CancellationToken cancellationToken);
}

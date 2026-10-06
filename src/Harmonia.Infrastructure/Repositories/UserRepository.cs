using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Harmonia.Domain.Common;

namespace Harmonia.Infrastructure.Repositories;

public class UserRepository(HarmoniaDbContext dbContext)
    : GenericRepository<User>(dbContext), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        DbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        DbContext.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken) =>
        await DbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);

    public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        DbContext.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task AddPasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken) =>
        await DbContext.PasswordResetTokens.AddAsync(passwordResetToken, cancellationToken);

    public async Task RemoveUnusedPasswordResetTokensAsync(Guid userId, CancellationToken cancellationToken) =>
        DbContext.PasswordResetTokens.RemoveRange(await DbContext.PasswordResetTokens
            .Where(x => x.UserId == userId && x.UsedAt == null)
            .ToListAsync(cancellationToken));

    public async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await DbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }
    }

    public override Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
    DbContext.Users
        .Include(x => x.Role)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId, CancellationToken ct) => 
        DbContext.Users.AnyAsync(x => x.Email == email && x.Id != excludeUserId, ct);

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken ct) =>
        DbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, ct);

    public Task<int> CountActiveAdminsAsync(CancellationToken ct) =>
        DbContext.Users.CountAsync(x => x.Role.Name == RoleNames.Admin && x.IsActive, ct);

    public Task<List<Guid>> GetActiveUserIdsByRolesAsync(
    IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        var roleNamesList = roleNames.ToList();

        return DbContext.Users
            .Where(x => EF.Constant(roleNamesList).Contains(x.Role.Name) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}

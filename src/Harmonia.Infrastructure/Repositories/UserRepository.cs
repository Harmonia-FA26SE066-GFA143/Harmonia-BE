using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.Data.Interceptors;
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

    public async Task<PagedList<(User User, int PasswordChangeCount, int PasswordResetCount, int AccountChangeCount, int ActionCount)>>
        GetActivityAsync(SearchReportsRequest filter, CancellationToken cancellationToken)
    {
        var logs = DbContext.AuditLogs.AsNoTracking();

        if (filter.FromDate is { } fromDate)
        {
            var fromUtc = VietnamTime.StartOfDayUtc(fromDate);
            logs = logs.Where(l => l.CreatedAt >= fromUtc);
        }

        if (filter.ToDate is { } toDate)
        {
            var toUtc = VietnamTime.StartOfDayUtc(toDate.AddDays(1));
            logs = logs.Where(l => l.CreatedAt < toUtc);
        }

        var aboutUsers = logs.Where(l => l.EntityType == nameof(User));

        var rows = await DbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ThenBy(u => u.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new
            {
                User = u,
                u.Role,
                PasswordChangeCount = aboutUsers.Count(l => l.EntityId == u.Id
                    && l.Action == AuditableEntityInterceptor.PasswordChangedAction && l.UserId != null),
                PasswordResetCount = aboutUsers.Count(l => l.EntityId == u.Id
                    && l.Action == AuditableEntityInterceptor.PasswordChangedAction && l.UserId == null),
                AccountChangeCount = aboutUsers.Count(l => l.EntityId == u.Id && l.Action == "Updated"),
                ActionCount = logs.Count(l => l.UserId == u.Id),
            })
            .ToListAsync(cancellationToken);

        // A projection drops Include, so the role is selected beside the user and attached here.
        foreach (var row in rows) row.User.Role = row.Role;

        return new PagedList<(User, int, int, int, int)>(
            rows.Select(x => (x.User, x.PasswordChangeCount, x.PasswordResetCount, x.AccountChangeCount, x.ActionCount)).ToList(),
            filter.PageNumber, filter.PageSize, await DbContext.Users.CountAsync(cancellationToken));
    }

    public async Task<PagedList<User>> SearchAsync(
        string? keyword, SearchUsersRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.Users.AsNoTracking();

        if (keyword is not null)
        {
            query = query.Where(x => x.Email.Contains(keyword));
        }

        if (filter.RoleName is not null)
        {
            query = query.Where(x => x.Role.Name == filter.RoleName);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(x => x.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Role)
            .OrderBy(x => x.Email)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<User>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        DbContext.RefreshTokens
            .Include(x => x.User.Role)
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

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId, CancellationToken cancellationToken) => 
        DbContext.Users.AnyAsync(x => x.Email == email && x.Id != excludeUserId, cancellationToken);

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken) =>
        DbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        DbContext.Users.CountAsync(x => x.Role.Name == RoleNames.Admin && x.IsActive, cancellationToken);

    public Task<List<Guid>> GetActiveUserIdsByRolesAsync(
    IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    {
        var roleNamesList = roleNames.ToList();

        return DbContext.Users
            .Where(x => EF.Constant(roleNamesList).Contains(x.Role.Name) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<User>> GetActiveByRoleAsync(string roleName, CancellationToken cancellationToken) =>
        DbContext.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Where(x => x.Role.Name == roleName && x.IsActive)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Harmonia.Domain.Common;
using System.Linq.Expressions;

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

        // Audit logs have no season, so a season narrows them to its dates (ANDed with the date range above).
        if (filter.LiturgicalSeasonId is { } seasonId)
        {
            var season = await DbContext.LiturgicalSeasons.AsNoTracking()
                .Where(s => s.Id == seasonId)
                .Select(s => new { s.CalendarKey, s.StartDate, s.EndDate })
                .FirstOrDefaultAsync(cancellationToken);
            List<(DateOnly From, DateOnly To)> ranges;
            if (season?.CalendarKey is { } key)
            {
                // A keyed season spans every year: its days are the synced days of that season.
                var days = await DbContext.LiturgicalDays.AsNoTracking()
                    .Where(d => d.SeasonName == key)
                    .OrderBy(d => d.Date)
                    .Select(d => d.Date)
                    .ToListAsync(cancellationToken);
                ranges = ToRanges(days);
            }
            else
            {
                ranges = season is { StartDate: { } start, EndDate: { } end } ? [(start, end)] : [];
            }

            logs = logs.Where(CreatedWithin(ranges));
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
            query = query.Where(x => x.Email.Contains(keyword) || x.FullName.Contains(keyword));
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

    public async Task<List<Guid>?> GetActiveUserIdsOfEventChoirsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var choirs = DbContext.LiturgicalEventChoirs.Where(x => x.EventId == eventId).Select(x => x.Choir);
        if (!await choirs.AnyAsync(cancellationToken))
        {
            return null;
        }

        var memberUserIds = choirs
            .SelectMany(c => c.Memberships)
            .Where(m => m.IsActive && m.Member.Status == MemberStatus.Active)
            .Select(m => m.Member.UserId);
        var directorIds = choirs.Where(c => c.DirectorId != null).Select(c => c.DirectorId!.Value);

        return await DbContext.Users
            .Where(u => u.IsActive && (memberUserIds.Contains(u.Id) || directorIds.Contains(u.Id)))
            .Select(u => u.Id)
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

    /// <summary>Collapses sorted dates into runs of consecutive days.</summary>
    private static List<(DateOnly From, DateOnly To)> ToRanges(List<DateOnly> sortedDates)
    {
        var ranges = new List<(DateOnly From, DateOnly To)>();
        foreach (var date in sortedDates)
        {
            if (ranges.Count > 0 && ranges[^1].To.AddDays(1) == date)
            {
                ranges[^1] = (ranges[^1].From, date);
            }
            else
            {
                ranges.Add((date, date));
            }
        }

        return ranges;
    }

    /// <summary>Log rows created on any of the parish days in <paramref name="ranges"/>; none when empty.</summary>
    private static Expression<Func<AuditLog, bool>> CreatedWithin(List<(DateOnly From, DateOnly To)> ranges)
    {
        var log = Expression.Parameter(typeof(AuditLog), "l");
        var createdAt = Expression.Property(log, nameof(AuditLog.CreatedAt));
        Expression body = Expression.Constant(false);
        foreach (var (from, to) in ranges)
        {
            body = Expression.OrElse(body, Expression.AndAlso(
                Expression.GreaterThanOrEqual(createdAt, Expression.Constant(VietnamTime.StartOfDayUtc(from))),
                Expression.LessThan(createdAt, Expression.Constant(VietnamTime.StartOfDayUtc(to.AddDays(1))))));
        }

        return Expression.Lambda<Func<AuditLog, bool>>(body, log);
    }
}

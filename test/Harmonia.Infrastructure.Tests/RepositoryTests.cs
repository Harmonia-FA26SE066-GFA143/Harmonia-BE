using Harmonia.Application.Common.Models;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    // ---- UserRepository ----

    [Fact]
    public async Task GetByEmailAsync_LoadsRole_Async()
    {
        await _db.AddUserAsync("a@test.com", RoleNames.ChoirDirector, cancellationToken: _ct);
        await using var context = _db.NewContext();

        var user = await new UserRepository(context).GetByEmailAsync("a@test.com", _ct);

        Assert.NotNull(user);
        Assert.Equal(RoleNames.ChoirDirector, user.Role.Name);
    }

    [Fact]
    public async Task GetByEmailAsync_Unknown_ReturnsNull_Async()
    {
        await using var context = _db.NewContext();

        Assert.Null(await new UserRepository(context).GetByEmailAsync("nobody@test.com", _ct));
    }

    [Fact]
    public async Task GetRefreshTokenByHashAsync_LoadsUserAndRole_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.RefreshTokens.Add(NewRefreshToken(user.Id, "h1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var token = await new UserRepository(context).GetRefreshTokenByHashAsync("h1", _ct);

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.User.Id);
        Assert.Equal(RoleNames.ChoirMember, token.User.Role.Name);
    }

    [Fact]
    public async Task RevokeAllRefreshTokensAsync_RevokesOnlyThatUsersActiveTokens_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var alreadyRevokedAt = DateTime.UtcNow.AddDays(-2);
        await using (var seed = _db.NewContext())
        {
            seed.RefreshTokens.AddRange(
                NewRefreshToken(user.Id, "u1"),
                NewRefreshToken(user.Id, "u2"),
                NewRefreshToken(user.Id, "u3", revokedAt: alreadyRevokedAt),
                NewRefreshToken(other.Id, "o1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new UserRepository(context);
            await repository.RevokeAllRefreshTokensAsync(user.Id, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        var tokens = await verify.RefreshTokens.ToDictionaryAsync(t => t.TokenHash, _ct);
        Assert.NotNull(tokens["u1"].RevokedAt);
        Assert.NotNull(tokens["u2"].RevokedAt);
        Assert.Equal(alreadyRevokedAt, tokens["u3"].RevokedAt);
        Assert.Null(tokens["o1"].RevokedAt);
    }

    [Fact]
    public async Task RemoveUnusedPasswordResetTokensAsync_KeepsUsedAndOtherUsersTokens_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.PasswordResetTokens.AddRange(
                NewResetToken(user.Id, "unused1"),
                NewResetToken(user.Id, "unused2"),
                NewResetToken(user.Id, "used", usedAt: DateTime.UtcNow.AddHours(-1)),
                NewResetToken(other.Id, "other"));
            await seed.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new UserRepository(context);
            await repository.RemoveUnusedPasswordResetTokensAsync(user.Id, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        var remaining = await verify.PasswordResetTokens.Select(t => t.TokenHash).OrderBy(h => h).ToListAsync(_ct);
        Assert.Equal(["other", "used"], remaining);
    }

    [Fact]
    public async Task GetPasswordResetTokenByHashAsync_LoadsUser_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.PasswordResetTokens.Add(NewResetToken(user.Id, "r1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var token = await new UserRepository(context).GetPasswordResetTokenByHashAsync("r1", _ct);

        Assert.NotNull(token);
        Assert.Equal("a@test.com", token.User.Email);
    }

    [Fact]
    public async Task Users_EmailIsUnique_Async()
    {
        await _db.AddUserAsync("a@test.com", cancellationToken: _ct);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.AddUserAsync("a@test.com", cancellationToken: _ct));
    }

    // ---- NotificationRepository ----

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyOwnRowsNewestFirstAndPaged_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var seed = _db.NewContext())
        {
            for (var i = 0; i < 5; i++)
            {
                seed.Notifications.Add(NewNotification($"mine-{i}", start.AddHours(i), user.Id));
            }

            seed.Notifications.Add(NewNotification("theirs", start.AddDays(1), other.Id));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var page = await new NotificationRepository(context)
            .GetForUserAsync(user.Id, new PagingRequest { PageNumber = 2, PageSize = 2 }, _ct);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["mine-2", "mine-1"], page.Items.Select(r => r.Notification.Title));
        Assert.All(page.Items, r => Assert.Equal(user.Id, r.UserId));
    }

    [Fact]
    public async Task GetRecipientAsync_AnotherUsersNotification_ReturnsNull_Async()
    {
        var owner = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var stranger = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var notification = NewNotification("n", DateTime.UtcNow, owner.Id);
        await using (var seed = _db.NewContext())
        {
            seed.Notifications.Add(notification);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new NotificationRepository(context);

        Assert.Null(await repository.GetRecipientAsync(notification.Id, stranger.Id, _ct));
        Assert.NotNull(await repository.GetRecipientAsync(notification.Id, owner.Id, _ct));
    }

    [Fact]
    public async Task CountUnreadAsync_CountsOnlyOwnUnread_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.Notifications.AddRange(
                NewNotification("1", DateTime.UtcNow, user.Id),
                NewNotification("2", DateTime.UtcNow, user.Id),
                NewNotification("3", DateTime.UtcNow, user.Id, isRead: true),
                NewNotification("4", DateTime.UtcNow, other.Id));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();

        Assert.Equal(2, await new NotificationRepository(context).CountUnreadAsync(user.Id, _ct));
    }

    // ---- GenericRepository ----

    [Fact]
    public async Task GenericRepository_AddGetRemove_RoundTrips_Async()
    {
        var id = Guid.NewGuid();
        await using (var context = _db.NewContext())
        {
            var repository = new GenericRepository<Role>(context);
            await repository.AddAsync(new Role { Id = id, Name = "Temp" }, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new GenericRepository<Role>(context);
            var role = await repository.GetByIdAsync(id, _ct);
            Assert.NotNull(role);
            repository.Remove(role);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        Assert.Null(await new GenericRepository<Role>(verify).GetByIdAsync(id, _ct));
    }

    private static RefreshToken NewRefreshToken(Guid userId, string hash, DateTime? revokedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = revokedAt,
        };

    private static PasswordResetToken NewResetToken(Guid userId, string hash, DateTime? usedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddHours(1), UsedAt = usedAt,
        };

    private static Notification NewNotification(string title, DateTime createdAt, Guid userId, bool isRead = false) =>
        new()
        {
            Id = Guid.NewGuid(), Title = title, Content = "c", CreatedAt = createdAt,
            Recipients = [new NotificationRecipient { Id = Guid.NewGuid(), UserId = userId, IsRead = isRead }],
        };
}

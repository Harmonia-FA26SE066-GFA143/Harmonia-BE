using Harmonia.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

public sealed class AuditableEntityInterceptorTests : IDisposable
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly TestDb _db = new();
    private readonly Guid _actorId;

    public AuditableEntityInterceptorTests()
    {
        // AuditLog.UserId is a foreign key, so the actor must be a real user.
        _actorId = _db.AddUserAsync("actor@test.com", "Admin", cancellationToken: _ct).GetAwaiter().GetResult().Id;
        _db.CurrentUser.UserId.Returns(_actorId);
        _db.CurrentUser.IpAddress.Returns("10.0.0.1");
    }

    public void Dispose() => _db.Dispose();

    private async Task<List<AuditLog>> LogsForAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        return await context.Set<AuditLog>()
            .Where(l => l.EntityId == entityId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    [Fact]
    public async Task Added_StampsCreatedAtAndBy_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);

        await using var context = _db.NewContext();
        var saved = await context.Users.SingleAsync(u => u.Id == user.Id, _ct);
        Assert.Equal(_actorId, saved.CreatedBy);
        Assert.True(saved.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
        Assert.Null(saved.UpdatedAt);
    }

    [Fact]
    public async Task Modified_StampsUpdatedAndKeepsCreated_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        DateTime createdAt;
        await using (var context = _db.NewContext())
        {
            var tracked = await context.Users.SingleAsync(u => u.Id == user.Id, _ct);
            createdAt = tracked.CreatedAt;
            tracked.AvatarUrl = "https://img/new.png";
            tracked.CreatedAt = DateTime.UnixEpoch; // must be ignored
            tracked.CreatedBy = Guid.NewGuid();     // must be ignored
            await context.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        var saved = await verify.Users.SingleAsync(u => u.Id == user.Id, _ct);
        Assert.Equal(createdAt, saved.CreatedAt);
        Assert.Equal(_actorId, saved.CreatedBy);
        Assert.Equal(_actorId, saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
    }

    [Fact]
    public async Task AuditedEntity_Created_WritesLogWithoutPasswordHash_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);

        var log = Assert.Single(await LogsForAsync(user.Id, _ct), l => l.EntityType == nameof(User));
        Assert.Equal("Created", log.Action);
        Assert.Equal(_actorId, log.UserId);
        Assert.Equal("10.0.0.1", log.IpAddress);
        Assert.Contains("a@test.com", log.NewValue);
        Assert.DoesNotContain("PasswordHash", log.NewValue);
        Assert.DoesNotContain("CreatedAt", log.NewValue);
    }

    [Fact]
    public async Task AuditedEntity_Updated_LogsOnlyChangedFields_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var context = _db.NewContext())
        {
            var tracked = await context.Users.SingleAsync(u => u.Id == user.Id, _ct);
            tracked.IsActive = false;
            tracked.PasswordHash = "new-hash";
            await context.SaveChangesAsync(_ct);
        }

        var log = Assert.Single(await LogsForAsync(user.Id, _ct), l => l.Action == "Updated");
        Assert.Contains("IsActive", log.NewValue);
        Assert.Contains("IsActive", log.OldValue);
        Assert.DoesNotContain("Email", log.NewValue);
        Assert.DoesNotContain("hash", log.NewValue);
        Assert.DoesNotContain("hash", log.OldValue);
    }

    [Fact]
    public async Task OnlyLastLoginAtChanged_WritesNoLog_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var context = _db.NewContext())
        {
            (await context.Users.SingleAsync(u => u.Id == user.Id, _ct)).LastLoginAt = DateTime.UtcNow;
            await context.SaveChangesAsync(_ct);
        }

        Assert.DoesNotContain(await LogsForAsync(user.Id, _ct), l => l.Action == "Updated");
    }

    [Fact]
    public async Task OnlyPasswordHashChanged_WritesNoLog_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var context = _db.NewContext())
        {
            (await context.Users.SingleAsync(u => u.Id == user.Id, _ct)).PasswordHash = "new-hash";
            await context.SaveChangesAsync(_ct);
        }

        Assert.DoesNotContain(await LogsForAsync(user.Id, _ct), l => l.Action == "Updated");
    }

    [Fact]
    public async Task NonAuditedEntity_WritesNoLog_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var tokenId = Guid.NewGuid();
        await using (var context = _db.NewContext())
        {
            context.RefreshTokens.Add(new RefreshToken
            {
                Id = tokenId, UserId = user.Id, TokenHash = "h", ExpiresAt = DateTime.UtcNow.AddDays(1),
            });
            await context.SaveChangesAsync(_ct);
        }

        Assert.Empty(await LogsForAsync(tokenId, _ct));
    }

    [Fact]
    public async Task AuditedEntity_Deleted_LogsOldValues_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var context = _db.NewContext())
        {
            context.Users.Remove(await context.Users.SingleAsync(u => u.Id == user.Id, _ct));
            await context.SaveChangesAsync(_ct);
        }

        var log = Assert.Single(await LogsForAsync(user.Id, _ct), l => l.Action == "Deleted");
        Assert.Null(log.NewValue);
        Assert.Contains("a@test.com", log.OldValue);
        Assert.DoesNotContain("PasswordHash", log.OldValue);
    }
}

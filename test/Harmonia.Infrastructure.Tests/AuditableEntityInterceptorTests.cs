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
    public async Task ReadBack_DateTimeIsMarkedUtc_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);

        await using var context = _db.NewContext();
        var saved = await context.Users.SingleAsync(u => u.Id == user.Id, _ct);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
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
    public async Task PasswordHashChanged_WritesPasswordChangedLogWithoutValues_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var context = _db.NewContext())
        {
            var tracked = await context.Users.SingleAsync(u => u.Id == user.Id, _ct);
            tracked.PasswordHash = "new-hash";
            tracked.FullName = "Renamed";
            await context.SaveChangesAsync(_ct);
        }

        var logs = await LogsForAsync(user.Id, _ct);
        var log = Assert.Single(logs, l => l.Action == Data.Interceptors.AuditableEntityInterceptor.PasswordChangedAction);
        Assert.Equal((nameof(User), _actorId, null, null), (log.EntityType, log.UserId, log.OldValue, log.NewValue));
        Assert.DoesNotContain("hash", Assert.Single(logs, l => l.Action == "Updated").NewValue);
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
    public async Task AdminCatalogs_AreAudited_Async()
    {
        var category = new SkillCategory { Id = Guid.NewGuid(), Name = "Dance" };
        var skill = new Skill { Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Sacred dance" };
        var massType = new MassType { Id = Guid.NewGuid(), Name = "Sunday Mass" };
        await using (var context = _db.NewContext())
        {
            context.AddRange(category, skill, massType);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            (await context.Set<Skill>().SingleAsync(s => s.Id == skill.Id, _ct)).IsActive = false;
            await context.SaveChangesAsync(_ct);
        }

        Assert.Equal(nameof(SkillCategory), Assert.Single(await LogsForAsync(category.Id, _ct)).EntityType);
        Assert.Equal(nameof(MassType), Assert.Single(await LogsForAsync(massType.Id, _ct)).EntityType);
        Assert.Equal(["Created", "Updated"], (await LogsForAsync(skill.Id, _ct)).Select(l => l.Action));
    }

    [Fact]
    public async Task MusicMaterial_SoftDeleted_LogsTheIsActiveChange_Async()
    {
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), Title = "Kyrie sheet", FilePublicId = "f.pdf", FileName = "kyrie.pdf",
            Song = new Song { Id = Guid.NewGuid(), Title = "Kyrie" },
        };
        await using (var context = _db.NewContext())
        {
            context.Add(material);
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            (await context.Set<MusicMaterial>().SingleAsync(m => m.Id == material.Id, _ct)).IsActive = false;
            await context.SaveChangesAsync(_ct);
        }

        var log = Assert.Single(await LogsForAsync(material.Id, _ct), l => l.Action == "Updated");
        Assert.Equal((nameof(MusicMaterial), """{"IsActive":true}""", """{"IsActive":false}"""), (log.EntityType, log.OldValue, log.NewValue));
    }

    [Fact]
    public async Task RehearsalAttendance_LogsOnlyAChangedStatus_Async()
    {
        var member = new MemberProfile
        {
            Id = Guid.NewGuid(), UserId = (await _db.AddUserAsync("m@test.com", cancellationToken: _ct)).Id,
        };
        var attendance = new RehearsalAttendance
        {
            Id = Guid.NewGuid(), MemberId = member.Id, CheckedBy = _actorId, CheckedAt = DateTime.UtcNow.AddHours(-1),
            Status = Domain.Enums.AttendanceStatus.Absent,
            Rehearsal = new Rehearsal { Id = Guid.NewGuid(), StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow },
        };
        async Task SaveAsync(Action<RehearsalAttendance> change)
        {
            await using var context = _db.NewContext();
            change(await context.Set<RehearsalAttendance>().SingleAsync(a => a.Id == attendance.Id, _ct));
            await context.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            context.AddRange(member, attendance);
            await context.SaveChangesAsync(_ct);
        }

        // Taking attendance, then saving the sheet again unchanged: neither is a correction.
        await SaveAsync(a => a.CheckedAt = DateTime.UtcNow);
        Assert.Empty(await LogsForAsync(attendance.Id, _ct));

        await SaveAsync(a =>
        {
            a.Status = Domain.Enums.AttendanceStatus.Excused;
            a.CheckedAt = DateTime.UtcNow;
        });

        var log = Assert.Single(await LogsForAsync(attendance.Id, _ct));
        Assert.Equal(
            ("Updated", """{"Status":"Absent"}""", """{"Status":"Excused"}""", _actorId),
            (log.Action, log.OldValue, log.NewValue, log.UserId!.Value));
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

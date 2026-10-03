using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

/// <summary>
/// A fresh SQLite in-memory database per instance, with the real model and the audit interceptor.
/// The database lives as long as the connection, so each test class instance gets a clean one.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();

    public TestDb()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// A new context over the same database, so reads don't hit the previous context's tracker.
    /// <paramref name="interceptors"/> run after the audit interceptor.
    /// </summary>
    public HarmoniaDbContext NewContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<HarmoniaDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors([new AuditableEntityInterceptor(CurrentUser), .. interceptors])
            .Options);

    public async Task<User> AddUserAsync(
        string email, string roleName = RoleNames.ChoirMember, CancellationToken cancellationToken = default)
    {
        await using var context = NewContext();
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            ?? context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = roleName }).Entity;
        var user = new User { Id = Guid.NewGuid(), Email = email, PasswordHash = "hash", RoleId = role.Id };
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public void Dispose() => _connection.Dispose();
}

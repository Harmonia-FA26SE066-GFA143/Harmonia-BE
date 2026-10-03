using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

public sealed class DataSeederTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public DataSeederTests()
    {
        _configuration["Seed:AdminEmail"].Returns("admin@test.com");
        _configuration["Seed:AdminPassword"].Returns("Passw0rd!");
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        await new DataSeeder(context, new PasswordHasherService(), _configuration, NullLogger<DataSeeder>.Instance)
            .SeedAsync(cancellationToken);
    }

    [Fact]
    public async Task SeedAsync_EmptyDatabase_CreatesFourRolesAndAdmin_Async()
    {
        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        Assert.Equal(
            [RoleNames.Admin, RoleNames.ChoirDirector, RoleNames.ChoirMember, RoleNames.ParishPriest],
            await context.Roles.Select(r => r.Name).OrderBy(n => n).ToListAsync(_ct));
        var admin = await context.Users.Include(u => u.Role).SingleAsync(_ct);
        Assert.Equal("admin@test.com", admin.Email);
        Assert.Equal(RoleNames.Admin, admin.Role.Name);
        Assert.True(admin.IsActive);
        Assert.True(new PasswordHasherService().VerifyPassword(admin, "Passw0rd!"));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_AddsNothingMore_Async()
    {
        await SeedAsync(_ct);
        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        Assert.Equal(4, await context.Roles.CountAsync(_ct));
        Assert.Equal(1, await context.Users.CountAsync(_ct));
    }

    [Fact]
    public async Task SeedAsync_ExistingRolesAndAdmin_KeepsThemUntouched_Async()
    {
        var existingAdmin = await _db.AddUserAsync("old-admin@test.com", RoleNames.Admin, _ct);

        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        Assert.Equal(existingAdmin.RoleId, (await context.Roles.SingleAsync(r => r.Name == RoleNames.Admin, _ct)).Id);
        Assert.Equal(4, await context.Roles.CountAsync(_ct));
        Assert.Equal("old-admin@test.com", (await context.Users.SingleAsync(_ct)).Email);
    }

    [Fact]
    public async Task SeedAsync_ChoirMemberWithoutProfile_GetsOne_OthersDoNot_Async()
    {
        var member = await _db.AddUserAsync("member@test.com", RoleNames.ChoirMember, _ct);
        await _db.AddUserAsync("director@test.com", RoleNames.ChoirDirector, _ct);

        await SeedAsync(_ct);
        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        var profile = await context.MemberProfiles.SingleAsync(_ct);
        Assert.Equal(member.Id, profile.UserId);
        Assert.Equal(MemberStatus.Active, profile.Status);
    }

    [Fact]
    public async Task SeedAsync_NoSeedKeys_CreatesRolesOnly_Async()
    {
        _configuration["Seed:AdminPassword"].Returns((string?)null);

        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        Assert.Equal(4, await context.Roles.CountAsync(_ct));
        Assert.False(await context.Users.AnyAsync(_ct));
    }
}

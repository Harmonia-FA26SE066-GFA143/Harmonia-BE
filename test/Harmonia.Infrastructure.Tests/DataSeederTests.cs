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
    public async Task SeedAsync_EmptyTranslations_LoadsTheBundledNamesOnce_AndNotAfterTheAdminDeletesOne_Async()
    {
        await SeedAsync(_ct);

        await using (var context = _db.NewContext())
        {
            var christmas = await context.LiturgicalNameTranslations
                .Where(x => x.EnglishName == "Christmas")
                .ToDictionaryAsync(x => x.Kind, x => x.VietnameseName, _ct);
            Assert.Equal(230, await context.LiturgicalNameTranslations.CountAsync(_ct));
            Assert.Equal("Chúa Giáng Sinh", christmas[LiturgicalNameKind.Celebration]);
            Assert.Equal("Mùa Giáng Sinh", christmas[LiturgicalNameKind.Season]);

            context.LiturgicalNameTranslations.Remove(await context.LiturgicalNameTranslations.FirstAsync(_ct));
            await context.SaveChangesAsync(_ct);
        }

        await SeedAsync(_ct);

        await using var verify = _db.NewContext();
        Assert.Equal(229, await verify.LiturgicalNameTranslations.CountAsync(_ct));
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
    public async Task SeedAsync_AddsMissingSystemSettings_AndKeepsValuesTheAdminChanged_Async()
    {
        await using (var seedContext = _db.NewContext())
        {
            seedContext.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(), Key = SystemSettingKeys.RosterRecentServiceDays, Value = "14", DataType = SettingDataType.Int,
            });
            await seedContext.SaveChangesAsync(_ct);
        }

        await SeedAsync(_ct);
        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        var settings = await context.SystemSettings.ToDictionaryAsync(x => x.Key, x => x.Value, _ct);
        Assert.Equal(SystemSettingKeys.All.Select(x => x.Key).Order(), settings.Keys.Order());
        Assert.Equal("14", settings[SystemSettingKeys.RosterRecentServiceDays]);
        Assert.Equal("true", settings[SystemSettingKeys.RosterAiSuggestionEnabled]);
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
    public async Task SeedAsync_AssignmentWithSingleMaterialColumn_GetsItAsItsFirstMaterialRow_Async()
    {
        var song = new Song { Id = Guid.NewGuid(), Title = "Song" };
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), SongId = song.Id, Title = "Sheet", MaterialType = MaterialType.SheetMusic,
            FilePublicId = "harmonia/sheet.pdf", FileName = "sheet.pdf",
        };
        PracticeAssignment Assignment(Guid? materialId) => new()
        {
            Id = Guid.NewGuid(), Title = "Practice", MaterialId = materialId, DueDate = DateTime.UtcNow.AddDays(1),
        };
        var old = Assignment(material.Id);
        var withoutMaterial = Assignment(null);
        await using (var seedContext = _db.NewContext())
        {
            seedContext.AddRange(song, material, old, withoutMaterial);
            await seedContext.SaveChangesAsync(_ct);
        }

        await SeedAsync(_ct);
        await SeedAsync(_ct);

        await using var context = _db.NewContext();
        var row = await context.PracticeAssignmentMaterials.SingleAsync(_ct);
        Assert.Equal((old.Id, material.Id, 0), (row.PracticeAssignmentId, row.MaterialId, row.DisplayOrder));
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

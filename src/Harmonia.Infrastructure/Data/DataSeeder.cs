using System.Text.Json;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Harmonia.Infrastructure.Data;

/// <summary>
/// Runs at startup and only ever inserts, so it is safe against any existing database.
/// Roles are matched by name, not seeded through HasData: deployed databases already hold
/// the four roles under random ids, and a HasData insert would collide on the unique Name index.
/// </summary>
public class DataSeeder(
    HarmoniaDbContext dbContext,
    IPasswordHasherService passwordHasherService,
    IConfiguration configuration,
    ILogger<DataSeeder> logger)
{
    private static readonly string[] RequiredRoleNames =
        [RoleNames.Admin, RoleNames.ParishPriest, RoleNames.ChoirDirector, RoleNames.ChoirMember];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedFirstAdminAsync(cancellationToken);
        await SeedMissingMemberProfilesAsync(cancellationToken);
        await SeedSystemSettingsAsync(cancellationToken);
        await SeedMissingAssignmentMaterialsAsync(cancellationToken);
        await SeedLiturgicalNameTranslationsAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the first Vietnamese calendar names (taken over from the Web's own table) into an empty table. Only once:
    /// afterwards the list is the Admin's, and a row the Admin deleted must not come back on the next start.
    /// </summary>
    private async Task SeedLiturgicalNameTranslationsAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.LiturgicalNameTranslations.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var stream = typeof(DataSeeder).Assembly.GetManifestResourceStream("LiturgicalNameTranslations.json")
            ?? throw new InvalidOperationException("Embedded resource LiturgicalNameTranslations.json is missing.");
        var byKind = await JsonSerializer.DeserializeAsync<Dictionary<LiturgicalNameKind, Dictionary<string, string>>>(
            stream, cancellationToken: cancellationToken) ?? [];

        dbContext.LiturgicalNameTranslations.AddRange(byKind.SelectMany(kind => kind.Value.Select(name =>
            new LiturgicalNameTranslation
            {
                Id = Guid.NewGuid(), Kind = kind.Key, EnglishName = name.Key, VietnameseName = name.Value,
            })));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} liturgical name translations.", byKind.Sum(x => x.Value.Count));
    }

    /// <summary>
    /// Assignments created when one material was a column of PracticeAssignment get that material as their first
    /// PracticeAssignmentMaterial row, so readers of the list see it too.
    /// </summary>
    private async Task SeedMissingAssignmentMaterialsAsync(CancellationToken cancellationToken)
    {
        var assignments = await dbContext.PracticeAssignments
            .Where(x => x.MaterialId != null && !x.Materials.Any())
            .Select(x => new { x.Id, MaterialId = x.MaterialId!.Value })
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return;
        }

        dbContext.PracticeAssignmentMaterials.AddRange(assignments.Select(x => new PracticeAssignmentMaterial
        {
            Id = Guid.NewGuid(), PracticeAssignmentId = x.Id, MaterialId = x.MaterialId, DisplayOrder = 0,
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} practice assignment materials from the single material column.", assignments.Count);
    }

    /// <summary>Adds the settings of SystemSettingKeys the database lacks. Existing rows keep the Admin's values.</summary>
    private async Task SeedSystemSettingsAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.SystemSettings.Select(x => x.Key).ToListAsync(cancellationToken);
        var missing = SystemSettingKeys.All.Where(x => !existing.Contains(x.Key)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        dbContext.SystemSettings.AddRange(missing.Select(x => new SystemSetting
        {
            Id = Guid.NewGuid(), Key = x.Key, Value = x.DefaultValue, DataType = x.DataType, Description = x.Description,
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded system settings: {Keys}", string.Join(", ", missing.Select(x => x.Key)));
    }

    /// <summary>ChoirMember accounts created before profiles were made automatically get one now.</summary>
    private async Task SeedMissingMemberProfilesAsync(CancellationToken cancellationToken)
    {
        var members = await dbContext.Users
            .Where(x => x.Role.Name == RoleNames.ChoirMember && x.MemberProfile == null)
            .Select(x => new { x.Id, x.CreatedAt })
            .ToListAsync(cancellationToken);
        if (members.Count == 0)
        {
            return;
        }

        dbContext.MemberProfiles.AddRange(members.Select(x => new MemberProfile
        {
            Id = Guid.NewGuid(),
            UserId = x.Id,
            JoinedDate = DateOnly.FromDateTime(x.CreatedAt),
            Status = MemberStatus.Active,
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} missing member profiles.", members.Count);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.Roles.Select(x => x.Name).ToListAsync(cancellationToken);
        var missing = RequiredRoleNames.Except(existing).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Roles.AddRange(missing.Select(name => new Role { Id = Guid.NewGuid(), Name = name }));
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded roles: {Roles}", string.Join(", ", missing));
    }

    private async Task SeedFirstAdminAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(x => x.Role.Name == RoleNames.Admin && x.IsActive, cancellationToken))
        {
            return;
        }

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No active Admin exists and Seed__AdminEmail / Seed__AdminPassword are not set; nobody can create accounts.");
            return;
        }

        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            logger.LogWarning("No active Admin exists, but the Seed__AdminEmail account already exists; not touching it.");
            return;
        }

        var adminRoleId = await dbContext.Roles
            .Where(x => x.Name == RoleNames.Admin)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);

        var admin = new User
        {
            Id = Guid.NewGuid(), Email = email, FullName = "Quản trị viên", RoleId = adminRoleId, IsActive = true,
        };
        admin.PasswordHash = passwordHasherService.HashPassword(admin, password);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded the first Admin account.");
    }
}

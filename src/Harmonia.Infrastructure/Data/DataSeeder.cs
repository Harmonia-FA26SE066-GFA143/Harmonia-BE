using System.Text.Json;
using System.Text.Json.Nodes;
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
        await SeedSongCatalogsAsync(cancellationToken);
    }

    /// <summary>
    /// Fills the Mass type, ceremony type, Mass part and song theme catalogs from SongCatalogs.json (classification
    /// modelled on dieuca.net). Each table only while it is empty, for the same reason as the translations.
    /// Liturgical seasons are not seeded: their dates change every year.
    /// </summary>
    private async Task SeedSongCatalogsAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(DataSeeder).Assembly.GetManifestResourceStream("SongCatalogs.json")
            ?? throw new InvalidOperationException("Embedded resource SongCatalogs.json is missing.");
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Embedded resource SongCatalogs.json is empty.");

        static IEnumerable<string> Names(JsonNode? node) => node!.AsArray().Select(x => x!.GetValue<string>());

        if (!await dbContext.MassTypes.AnyAsync(cancellationToken))
            dbContext.MassTypes.AddRange(Names(root["MassTypes"]).Select(x => new MassType { Id = Guid.NewGuid(), Name = x }));

        if (!await dbContext.CeremonyTypes.AnyAsync(cancellationToken))
            dbContext.CeremonyTypes.AddRange(Names(root["CeremonyTypes"]).Select(x => new CeremonyType { Id = Guid.NewGuid(), Name = x }));

        if (!await dbContext.LiturgicalSlots.AnyAsync(cancellationToken))
            dbContext.LiturgicalSlots.AddRange(Names(root["LiturgicalSlots"])
                .Select((x, i) => new LiturgicalSlot { Id = Guid.NewGuid(), Name = x, DefaultOrder = i + 1 }));

        if (!await dbContext.SongThemes.AnyAsync(cancellationToken))
            AddThemes(root["SongThemes"]!.AsObject(), null);

        var count = await dbContext.SaveChangesAsync(cancellationToken);
        if (count > 0) logger.LogInformation("Seeded {Count} song catalog rows.", count);

        await SeedSlotRequirementsAsync(root["LiturgicalSlotRequirements"]!.AsObject(), cancellationToken);

        // Each property is a theme; its value holds the child themes, nested to any depth.
        void AddThemes(JsonObject themes, Guid? parentId)
        {
            foreach (var (name, children) in themes)
            {
                var theme = new SongTheme { Id = Guid.NewGuid(), Name = name, ParentId = parentId };
                dbContext.SongThemes.Add(theme);
                AddThemes(children!.AsObject(), theme.Id);
            }
        }
    }

    /// <summary>
    /// Gives the named slots one person of the named skill category (key of SkillCategoryIds), e.g. a psalmist for the
    /// psalm, while the table is empty. Skills are the Admin's, so a slot is skipped until its category has an active
    /// skill, and the seed is retried on the next start.
    /// </summary>
    private async Task SeedSlotRequirementsAsync(JsonObject categoryBySlot, CancellationToken cancellationToken)
    {
        if (await dbContext.LiturgicalSlotRequirements.AnyAsync(cancellationToken)) return;

        foreach (var (slotName, categoryKey) in categoryBySlot)
        {
            var categoryId = (Guid)typeof(SkillCategoryIds).GetField(categoryKey!.GetValue<string>())!.GetValue(null)!;
            var slotId = await dbContext.LiturgicalSlots.Where(x => x.Name == slotName).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
            var skillId = await dbContext.Skills.Where(x => x.CategoryId == categoryId && x.IsActive)
                .OrderBy(x => x.Name).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
            if (slotId is null || skillId is null) continue;

            dbContext.LiturgicalSlotRequirements.Add(new LiturgicalSlotRequirement
            {
                Id = Guid.NewGuid(), SlotId = slotId.Value, SkillId = skillId.Value, RequiredCount = 1,
            });
        }

        var count = await dbContext.SaveChangesAsync(cancellationToken);
        if (count > 0) logger.LogInformation("Seeded {Count} liturgical slot requirements.", count);
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

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
            Id = Guid.NewGuid(), Email = email, FullName = "Administrator", RoleId = adminRoleId, IsActive = true,
        };
        admin.PasswordHash = passwordHasherService.HashPassword(admin, password);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded the first Admin account.");
    }
}

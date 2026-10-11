using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;

namespace Harmonia.API.Tests;

public class LookupsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private Task<int> SeedAsync(Action<HarmoniaDbContext> add, CancellationToken cancellationToken = default) =>
        factory.WithDbAsync(
            (db, ct) =>
            {
                add(db);
                return db.SaveChangesAsync(ct);
            },
            cancellationToken);

    [Fact]
    public async Task MassTypes_ReturnsActiveOnly_OrderedByName_Async()
    {
        await SeedAsync(db => db.MassTypes.AddRange(
            new MassType { Id = Guid.NewGuid(), Name = "Sunday Mass" },
            new MassType { Id = Guid.NewGuid(), Name = "Daily Mass" },
            new MassType { Id = Guid.NewGuid(), Name = "Retired Mass", IsActive = false }), _ct);
        var member = await factory.CreateClientAsAsync("member@test.com", _ct);

        var rows = (await member.GetFromJsonAsync<List<MassTypeDto>>("api/lookups/mass-types", TestJson.Options, _ct))!;

        Assert.Equal(["Daily Mass", "Sunday Mass"], rows.Select(x => x.Name));
    }

    [Fact]
    public async Task LiturgicalSlots_OrderedByDefaultOrder_Async()
    {
        await SeedAsync(db => db.LiturgicalSlots.AddRange(
            new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Communion", DefaultOrder = 4 },
            new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Entrance", DefaultOrder = 1 },
            new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Offertory", DefaultOrder = 3 }), _ct);
        var priest = await factory.CreateClientAsAsync("priest@test.com", _ct);

        var rows = (await priest.GetFromJsonAsync<List<LiturgicalSlotDto>>(
            "api/lookups/liturgical-slots", TestJson.Options, _ct))!;

        Assert.Equal(["Entrance", "Offertory", "Communion"], rows.Select(x => x.Name));
    }

    [Fact]
    public async Task Skills_FilterByCategory_HidesInactive_Async()
    {
        await SeedAsync(db => db.Skills.AddRange(
            new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Soprano" },
            new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "Organ" },
            new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "Harp", IsActive = false }), _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var instruments = (await director.GetFromJsonAsync<List<SkillDto>>(
            $"api/lookups/skills?categoryId={SkillCategoryIds.Instrument}", TestJson.Options, _ct))!;
        var categories = (await director.GetFromJsonAsync<List<SkillCategoryDto>>(
            "api/lookups/skill-categories", TestJson.Options, _ct))!;

        Assert.Equal("Organ", Assert.Single(instruments).Name);
        Assert.Equal(5, categories.Count);
    }

    // ---- Admin configures the skill catalog (UC-32 / FE-49) ----

    [Fact]
    public async Task SkillCatalog_AdminCreatesAndUpdates_OtherRolesAreForbidden_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var createdCategory = await admin.PostAsJsonAsync("api/lookups/skill-categories", new { name = "Dance" }, _ct);
        var category = (await createdCategory.Content.ReadFromJsonAsync<SkillCategoryDto>(TestJson.Options, _ct))!;
        var createdSkill = await admin.PostAsJsonAsync(
            "api/lookups/skills", new { categoryId = category.Id, name = "Liturgical dance" }, _ct);
        var skill = (await createdSkill.Content.ReadFromJsonAsync<SkillDto>(TestJson.Options, _ct))!;
        var renamedSkill = await admin.PutAsJsonAsync(
            $"api/lookups/skills/{skill.Id}", new { categoryId = category.Id, name = "Sacred dance", isActive = false }, _ct);
        var updated = (await renamedSkill.Content.ReadFromJsonAsync<SkillDto>(TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, createdCategory.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createdSkill.StatusCode);
        Assert.Equal(("Sacred dance", false), (updated.Name, updated.IsActive));

        var body = new { categoryId = category.Id, name = "Blocked" };
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PostAsJsonAsync("api/lookups/skill-categories", body, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PutAsJsonAsync($"api/lookups/skill-categories/{category.Id}", body, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PostAsJsonAsync("api/lookups/skills", body, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PutAsJsonAsync($"api/lookups/skills/{skill.Id}", body, _ct)).StatusCode);

        // An Admin-made category can be switched off; this also leaves the shared database with its five active ones.
        var switchedOff = await admin.PutAsJsonAsync(
            $"api/lookups/skill-categories/{category.Id}", new { name = "Dance", isActive = false }, _ct);
        Assert.False((await switchedOff.Content.ReadFromJsonAsync<SkillCategoryDto>(TestJson.Options, _ct))!.IsActive);
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response, string code, CancellationToken cancellationToken = default)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    [Fact]
    public async Task SkillCatalog_Guards_ReturnTheirCodes_Async()
    {
        var offCategory = new SkillCategory { Id = Guid.NewGuid(), Name = "Retired group", IsActive = false };
        // Inactive so the shared database keeps showing the same active skills to the other tests of this class.
        var tambourine = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "Tambourine", IsActive = false };
        await SeedAsync(db =>
        {
            db.AddRange(offCategory, tambourine);
            db.Add(new SongInstrumentRequirement
            {
                Id = Guid.NewGuid(), SkillId = tambourine.Id, Song = new Song { Id = Guid.NewGuid(), Title = "Guard song" },
            });
        }, _ct);
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var seededOff = await admin.PutAsJsonAsync(
            $"api/lookups/skill-categories/{SkillCategoryIds.Psalm}", new { name = "Psalm", isActive = false }, _ct);
        var intoOff = await admin.PostAsJsonAsync(
            "api/lookups/skills", new { categoryId = offCategory.Id, name = "Orphan" }, _ct);
        var acrossBoundary = await admin.PutAsJsonAsync(
            $"api/lookups/skills/{tambourine.Id}", new { categoryId = SkillCategoryIds.Vocal, name = "Tambourine" }, _ct);

        await AssertConflictAsync(seededOff, ErrorCodes.LookupInUse, _ct);
        await AssertConflictAsync(intoOff, ErrorCodes.LookupInactive, _ct);
        await AssertConflictAsync(acrossBoundary, ErrorCodes.SkillInUse, _ct);
    }

    [Fact]
    public async Task Anonymous_Returns401_Async()
    {
        var response = await factory.CreateClient().GetAsync("api/lookups/mass-types", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

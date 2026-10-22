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
            $"api/lookups/skill-categories/{SkillCategoryIds.Psalm}", new { name = "Đáp ca", isActive = false }, _ct);
        var intoOff = await admin.PostAsJsonAsync(
            "api/lookups/skills", new { categoryId = offCategory.Id, name = "Orphan" }, _ct);
        var acrossBoundary = await admin.PutAsJsonAsync(
            $"api/lookups/skills/{tambourine.Id}", new { categoryId = SkillCategoryIds.Vocal, name = "Tambourine" }, _ct);

        await AssertConflictAsync(seededOff, ErrorCodes.LookupInUse, _ct);
        await AssertConflictAsync(intoOff, ErrorCodes.LookupInactive, _ct);
        await AssertConflictAsync(acrossBoundary, ErrorCodes.SkillInUse, _ct);
    }

    // ---- Admin configures the other catalogs (UC-32 / FE-50) ----

    // Rows are created switched off so the shared database keeps showing the same active rows to the read tests above.
    public static TheoryData<string, object> CatalogBodies => new()
    {
        { "mass-types", new { name = "Write-test mass", isActive = false } },
        { "ceremony-types", new { name = "Write-test ceremony", isActive = false } },
        { "event-categories", new { name = "Write-test category", isActive = false } },
        { "song-themes", new { name = "Write-test theme", isActive = false } },
        { "worship-locations", new { name = "Write-test location", address = "1 Test St", isActive = false } },
        { "liturgical-seasons", new { name = "Write-test season", startDate = "2040-01-01", endDate = "2040-02-01", isActive = false } },
    };

    [Theory]
    [MemberData(nameof(CatalogBodies))]
    public async Task Catalog_AdminCreatesUpdatesAndSeesInactiveRows_OtherRolesAreForbidden_Async(string catalog, object body)
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var created = await admin.PostAsJsonAsync($"api/lookups/{catalog}", body, _ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(_ct)).GetProperty("id").GetGuid();

        var updated = await admin.PutAsJsonAsync($"api/lookups/{catalog}/{id}", body, _ct);
        var missing = await admin.PutAsJsonAsync($"api/lookups/{catalog}/{Guid.NewGuid()}", body, _ct);
        var withInactive = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>($"api/lookups/{catalog}?includeInactive=true", _ct);
        var asDirector = await director.GetFromJsonAsync<System.Text.Json.JsonElement>($"api/lookups/{catalog}?includeInactive=true", _ct);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(withInactive.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id && !x.GetProperty("isActive").GetBoolean());
        // The flag is ignored for other roles, so the switched-off row stays hidden.
        Assert.DoesNotContain(asDirector.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PostAsJsonAsync($"api/lookups/{catalog}", body, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PutAsJsonAsync($"api/lookups/{catalog}/{id}", body, _ct)).StatusCode);
    }

    [Fact]
    public async Task LiturgicalSlots_DirectorAndAdminWrite_PriestIsForbidden_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var priest = await factory.CreateClientAsAsync("priest@test.com", _ct);
        var body = new { name = "Write-test slot", defaultOrder = 9, isActive = false };

        var created = await director.PostAsJsonAsync("api/lookups/liturgical-slots", body, _ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(_ct)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await director.PutAsJsonAsync($"api/lookups/liturgical-slots/{id}", body, _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await priest.PostAsJsonAsync("api/lookups/liturgical-slots", body, _ct)).StatusCode);
    }

    [Fact]
    public async Task Catalog_DuplicateNameAndOverlappingSeason_ReturnTheirCodes_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var theme = new { name = "Duplicate-test theme", isActive = false };
        object Season(string start, string end) => new { name = "Overlap-test season", startDate = start, endDate = end };

        await admin.PostAsJsonAsync("api/lookups/song-themes", theme, _ct);
        var duplicate = await admin.PostAsJsonAsync("api/lookups/song-themes", theme, _ct);
        var first = await admin.PostAsJsonAsync("api/lookups/liturgical-seasons", Season("2041-03-01", "2041-04-01"), _ct);
        var overlapping = await admin.PostAsJsonAsync("api/lookups/liturgical-seasons", Season("2041-03-15", "2041-05-01"), _ct);
        var backwards = await admin.PostAsJsonAsync("api/lookups/liturgical-seasons", Season("2042-03-15", "2042-03-01"), _ct);

        await AssertConflictAsync(duplicate, ErrorCodes.LookupNameDuplicate, _ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertConflictAsync(overlapping, ErrorCodes.LiturgicalSeasonDateOverlap, _ct);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains(ErrorCodes.LiturgicalSeasonDateInvalid, await backwards.Content.ReadAsStringAsync(_ct));
    }

    [Fact]
    public async Task Anonymous_Returns401_Async()
    {
        var response = await factory.CreateClient().GetAsync("api/lookups/mass-types", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

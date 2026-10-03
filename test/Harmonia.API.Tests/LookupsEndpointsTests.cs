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

    [Fact]
    public async Task Anonymous_Returns401_Async()
    {
        var response = await factory.CreateClient().GetAsync("api/lookups/mass-types", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

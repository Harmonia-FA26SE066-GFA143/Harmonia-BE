using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.API.Tests;

public class LiturgicalNameTranslationsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdminEditsATranslation_AndTheDayShowsIt_OtherRolesCannotEdit_Async()
    {
        var date = new DateOnly(2027, 3, 19);
        await factory.WithDbAsync((db, ct) =>
        {
            db.LiturgicalDays.Add(new LiturgicalDay { Id = Guid.NewGuid(), Date = date, CelebrationName = "Saint Joseph Test" });
            return db.SaveChangesAsync(ct);
        }, _ct);
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var member = await factory.CreateClientAsAsync("member@test.com", _ct);
        var body = new { kind = "Celebration", englishName = "Saint Joseph Test", vietnameseName = "Thánh Giuse" };

        var missingBefore = await admin.GetStringAsync("api/liturgical-name-translations/missing", _ct);
        var created = await admin.PostAsJsonAsync("api/liturgical-name-translations", body, _ct);
        var duplicate = await admin.PostAsJsonAsync("api/liturgical-name-translations", body, _ct);
        var asMember = await member.PostAsJsonAsync("api/liturgical-name-translations", body, _ct);
        var day = await member.GetFromJsonAsync<JsonElement>($"api/liturgical-days/{date:yyyy-MM-dd}", _ct);

        Assert.Contains("Saint Joseph Test", missingBefore);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(ErrorCodes.LiturgicalNameTranslationDuplicate, await duplicate.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.Forbidden, asMember.StatusCode);
        Assert.Equal("Thánh Giuse", day.GetProperty("celebrationNameVi").GetString());
        Assert.Equal("Saint Joseph Test", day.GetProperty("celebrationName").GetString());
    }
}

using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.API.Tests;

public class SystemSettingsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private async Task<List<SystemSettingDto>> GetAsync(HttpClient client, string path, CancellationToken cancellationToken = default) =>
        (await client.GetFromJsonAsync<List<SystemSettingDto>>(path, TestJson.Options, cancellationToken))!;

    [Fact]
    public async Task Read_AdminSeesEverySetting_OtherRolesOnlyThePublicOnes_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var member = await factory.CreateClientAsAsync("member@test.com", _ct);

        var all = await GetAsync(admin, "api/system-settings", _ct);
        var visible = await GetAsync(member, "api/system-settings/public", _ct);
        var allAsMember = await member.GetAsync("api/system-settings", _ct);

        Assert.Equal(SystemSettingKeys.All.Select(x => x.Key).Order(StringComparer.Ordinal), all.Select(x => x.Key));
        Assert.Equal(
            [SystemSettingKeys.ChoirName, SystemSettingKeys.ParishName, SystemSettingKeys.SystemAnnouncement],
            visible.Select(x => x.Key));
        Assert.Equal(HttpStatusCode.Forbidden, allAsMember.StatusCode);
    }

    [Fact]
    public async Task Update_AdminChangesAValue_AndBadValuesOrOtherRolesAreRejected_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var days = (await GetAsync(admin, "api/system-settings", _ct)).Single(x => x.Key == SystemSettingKeys.RosterRecentServiceDays);
        Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string value) =>
            client.PutAsJsonAsync($"api/system-settings/{id}", new { value }, _ct);

        var updated = await PutAsync(admin, days.Id, "30");
        var outOfRange = await PutAsync(admin, days.Id, "0");
        var wrongType = await PutAsync(admin, days.Id, "many");
        var missing = await PutAsync(admin, Guid.NewGuid(), "30");
        var asDirector = await PutAsync(director, days.Id, "90");

        Assert.Equal("30", (await updated.Content.ReadFromJsonAsync<SystemSettingDto>(TestJson.Options, _ct))!.Value);
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        Assert.Contains(ErrorCodes.SettingValueOutOfRange, await outOfRange.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Contains(ErrorCodes.SettingValueTypeMismatch, await wrongType.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asDirector.StatusCode);

        // Rejected writes left the accepted value in place.
        Assert.Equal("30", (await GetAsync(admin, "api/system-settings", _ct)).Single(x => x.Id == days.Id).Value);
    }
}

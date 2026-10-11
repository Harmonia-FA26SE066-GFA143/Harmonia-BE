using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Domain.Common;

namespace Harmonia.API.Tests;

public class ReportsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private static readonly string[] AdminReports =
        ["user-activity", "rehearsal-attendance", "participation", "assignment-completion"];

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdminReports_OnlyTheAdminGetsIn_Async()
    {
        var admin = await factory.AddUserAsync("reports-admin@test.com", RoleNames.Admin, cancellationToken: _ct);
        var priest = await factory.AddUserAsync("reports-priest@test.com", RoleNames.ParishPriest, cancellationToken: _ct);
        var adminClient = await factory.CreateClientAsAsync(admin.Email, _ct);
        var priestClient = await factory.CreateClientAsAsync(priest.Email, _ct);

        foreach (var report in AdminReports)
        {
            Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync($"api/reports/{report}", _ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await priestClient.GetAsync($"api/reports/{report}", _ct)).StatusCode);
        }

        // The Parish Priest keeps the two UC-16 reports, now shared with the Admin.
        Assert.Equal(HttpStatusCode.OK, (await priestClient.GetAsync("api/reports/song-usage", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("api/reports/song-usage", _ct)).StatusCode);
    }

    [Fact]
    public async Task UserActivity_AfterAPasswordChange_CountsIt_Async()
    {
        var admin = await factory.AddUserAsync("reports-admin2@test.com", RoleNames.Admin, cancellationToken: _ct);
        var member = await factory.AddUserAsync("reports-member@test.com", cancellationToken: _ct);
        var memberClient = await factory.CreateClientAsAsync(member.Email, _ct);
        var change = await memberClient.PostAsJsonAsync(
            "api/auth/change-password",
            new { currentPassword = HarmoniaApiFactory.Password, newPassword = "BrandNew123" }, _ct);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var adminClient = await factory.CreateClientAsAsync(admin.Email, _ct);
        var page = await adminClient.GetFromJsonAsync<JsonElement>("api/reports/user-activity?pageSize=100", _ct);

        var row = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("email").GetString() == member.Email);
        Assert.Equal(1, row.GetProperty("passwordChangeCount").GetInt32());
        Assert.Equal(0, row.GetProperty("passwordResetCount").GetInt32());
        Assert.Equal(RoleNames.ChoirMember, row.GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task Reports_FromDateAfterToDate_Returns400_Async()
    {
        var admin = await factory.AddUserAsync("reports-admin3@test.com", RoleNames.Admin, cancellationToken: _ct);
        var client = await factory.CreateClientAsAsync(admin.Email, _ct);

        var response = await client.GetAsync("api/reports/participation?fromDate=2026-10-02&toDate=2026-10-01", _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.ReportDateRangeInvalid, await response.Content.ReadAsStringAsync(_ct));
    }
}

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
    public async Task Export_AsAdmin_DownloadsTheFile_AndListsItInTheHistory_Async()
    {
        var admin = await factory.AddUserAsync("reports-export@test.com", RoleNames.Admin, cancellationToken: _ct);
        var client = await factory.CreateClientAsAsync(admin.Email, _ct);

        var export = await client.GetAsync("api/reports/export?type=UserActivity&format=Csv", _ct);

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("text/csv", export.Content.Headers.ContentType!.MediaType);
        Assert.Matches(@"^UserActivity-\d{8}-\d{6}\.csv$", export.Content.Headers.ContentDisposition!.FileNameStar);
        var csv = await export.Content.ReadAsStringAsync(_ct);
        Assert.Contains("Họ tên,Email,Vai trò", csv);
        Assert.Contains(admin.Email, csv);

        var history = await client.GetFromJsonAsync<JsonElement>("api/reports/exports", _ct);
        var row = history.GetProperty("items").EnumerateArray().First();
        Assert.Equal("UserActivity", row.GetProperty("reportType").GetString());
        Assert.Equal("Csv", row.GetProperty("format").GetString());
        Assert.Equal(admin.Id, row.GetProperty("exportedBy").GetGuid());
        Assert.StartsWith("https://files.test/harmonia/report-exports/", row.GetProperty("downloadUrl").GetString());
    }

    [Theory]
    [InlineData("Xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("Pdf", "application/pdf")]
    [InlineData("Xml", "text/xml")]
    [InlineData("Json", "application/json")]
    public async Task Export_OtherFormats_ReturnTheirContentType_Async(string format, string contentType)
    {
        var admin = await factory.AddUserAsync($"reports-{format}@test.com", RoleNames.Admin, cancellationToken: _ct);
        var client = await factory.CreateClientAsAsync(admin.Email, _ct);

        var export = await client.GetAsync($"api/reports/export?type=UserActivity&format={format}", _ct);

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal(contentType, export.Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await export.Content.ReadAsByteArrayAsync(_ct));
    }

    [Fact]
    public async Task Export_RejectsOtherRolesBadTypesAndEmptyReports_Async()
    {
        var admin = await factory.AddUserAsync("reports-export2@test.com", RoleNames.Admin, cancellationToken: _ct);
        var priest = await factory.AddUserAsync("reports-priest2@test.com", RoleNames.ParishPriest, cancellationToken: _ct);
        var adminClient = await factory.CreateClientAsAsync(admin.Email, _ct);
        var priestClient = await factory.CreateClientAsAsync(priest.Email, _ct);

        var asPriest = await priestClient.GetAsync("api/reports/export?type=SongUsage", _ct);
        var historyAsPriest = await priestClient.GetAsync("api/reports/exports", _ct);
        var noType = await adminClient.GetAsync("api/reports/export", _ct);
        // No event exists in this fixture, so the service history has no rows.
        var empty = await adminClient.GetAsync("api/reports/export?type=ServiceHistory", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, asPriest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, historyAsPriest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noType.StatusCode);
        Assert.Contains(ErrorCodes.ReportTypeNotSupported, await noType.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Contains(ErrorCodes.ReportNoData, await empty.Content.ReadAsStringAsync(_ct));
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

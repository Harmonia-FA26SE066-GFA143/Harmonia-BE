using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Domain.Common;

namespace Harmonia.API.Tests;

public class AuditLogsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_AsAdmin_ShowsWhoDidWhat_WithoutSecrets_Async()
    {
        var admin = await factory.AddUserAsync("audit-admin@test.com", RoleNames.Admin, cancellationToken: _ct);
        var member = await factory.AddUserAsync("audit-member@test.com", cancellationToken: _ct);
        var client = await factory.CreateClientAsAsync(admin.Email, _ct);
        var deactivate = await client.PatchAsync($"api/users/{member.Id}/deactivate", null, _ct);
        Assert.True(deactivate.IsSuccessStatusCode, $"deactivate returned {(int)deactivate.StatusCode}");

        var response = await client.GetAsync($"api/audit-logs?entityType=User&entityId={member.Id}&action=Updated", _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(_ct);
        var row = JsonDocument.Parse(body).RootElement.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(admin.Id, row.GetProperty("userId").GetGuid());
        Assert.Equal(admin.Email, row.GetProperty("userEmail").GetString());
        Assert.Equal("""{"IsActive":false}""", row.GetProperty("newValue").GetString());
        Assert.DoesNotContain("PasswordHash", body);
    }

    [Fact]
    public async Task Search_OtherRolesAreForbidden_AndABackwardsRangeIsRejected_Async()
    {
        var admin = await factory.AddUserAsync("audit-admin2@test.com", RoleNames.Admin, cancellationToken: _ct);
        var priest = await factory.AddUserAsync("audit-priest@test.com", RoleNames.ParishPriest, cancellationToken: _ct);
        var adminClient = await factory.CreateClientAsAsync(admin.Email, _ct);
        var priestClient = await factory.CreateClientAsAsync(priest.Email, _ct);

        var asPriest = await priestClient.GetAsync("api/audit-logs", _ct);
        var backwards = await adminClient.GetAsync("api/audit-logs?fromDate=2026-10-02&toDate=2026-10-01", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, asPriest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains(ErrorCodes.ReportDateRangeInvalid, await backwards.Content.ReadAsStringAsync(_ct));
    }
}

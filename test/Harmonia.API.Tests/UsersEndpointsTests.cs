using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using NSubstitute;

namespace Harmonia.API.Tests;

public class UsersEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Page(List<UserDto> Items, int TotalCount);

    /// <summary>The generated first password, read back from the welcome email the server sent.</summary>
    private string EmailedPassword(string email)
    {
        var body = factory.EmailSender.ReceivedCalls()
            .Select(c => c.GetArguments())
            .Last(a => (string)a[0]! == email)[2] as string;
        return System.Text.RegularExpressions.Regex.Match(body!, "Password: <b>([^<]+)</b>").Groups[1].Value;
    }

    [Fact]
    public async Task Search_AsAdmin_FiltersByRoleAndActive_Async()
    {
        await factory.AddUserAsync("users-inactive@test.com", RoleNames.ChoirMember, isActive: false, _ct);
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var page = (await admin.GetFromJsonAsync<Page>(
            "api/users?roleName=ChoirMember&isActive=true&pageSize=100", TestJson.Options, _ct))!;

        Assert.Contains(page.Items, u => u.Email == "member@test.com");
        Assert.DoesNotContain(page.Items, u => u.Email == "users-inactive@test.com");
        Assert.All(page.Items, u => Assert.Equal(RoleNames.ChoirMember, u.RoleName));
        Assert.Equal(page.Items.Count, page.TotalCount);
    }

    [Fact]
    public async Task Search_KeywordMatchesPartOfEmail_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var page = (await admin.GetFromJsonAsync<Page>("api/users?keyword=priest", TestJson.Options, _ct))!;

        Assert.Equal("priest@test.com", Assert.Single(page.Items).Email);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsUser_Unknown_Returns404_Async()
    {
        var user = await factory.AddUserAsync("users-get@test.com", RoleNames.ChoirDirector, cancellationToken: _ct);
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var dto = (await admin.GetFromJsonAsync<UserDto>($"api/users/{user.Id}", TestJson.Options, _ct))!;
        var missing = await admin.GetAsync($"api/users/{Guid.NewGuid()}", _ct);

        Assert.Equal(RoleNames.ChoirDirector, dto.RoleName);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Create_WithFullName_ShowsInGetByIdAndLogin_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var created = await admin.PostAsJsonAsync(
            "api/users",
            new { email = "users-named@test.com", fullName = " Maria Nguyen ", roleName = RoleNames.ChoirDirector },
            _ct);
        var id = (await created.Content.ReadFromJsonAsync<UserDto>(TestJson.Options, _ct))!.Id;
        await factory.CompleteFirstSignInAsync("users-named@test.com", _ct);
        var fetched = (await admin.GetFromJsonAsync<UserDto>($"api/users/{id}", TestJson.Options, _ct))!;
        var login = await factory.LoginAsync("users-named@test.com", cancellationToken: _ct);

        Assert.True(created.IsSuccessStatusCode);
        Assert.Equal("Maria Nguyen", fetched.FullName);
        Assert.Equal("Maria Nguyen", login.User.FullName);
    }

    [Fact]
    public async Task Create_WithoutFullName_StoresEmptyNameAndPhone_EmailsPassword_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var response = await admin.PostAsJsonAsync(
            "api/users",
            new { email = "users-noname@test.com", phone = " 0901234567 ", roleName = RoleNames.ChoirMember },
            _ct);
        var created = (await response.Content.ReadFromJsonAsync<UserDto>(TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, created.FullName);
        Assert.Equal("0901234567", created.Phone);
        Assert.True(created.IsPasswordChangeRequired);
        Assert.Matches("^[A-Za-z2-9]{12}$", EmailedPassword("users-noname@test.com"));
    }

    [Fact]
    public async Task Create_BlocksUntilPasswordChanged_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        await admin.PostAsJsonAsync(
            "api/users",
            new { email = "users-firstlogin@test.com", roleName = RoleNames.ParishPriest },
            _ct);

        var emailed = EmailedPassword("users-firstlogin@test.com");
        var first = await factory.LoginAsync("users-firstlogin@test.com", emailed, _ct);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        var blocked = await client.GetAsync("api/lookups/mass-types", _ct);
        var me = await client.GetAsync("api/auth/me", _ct);
        var change = await client.PostAsJsonAsync(
            "api/auth/change-password", new { currentPassword = emailed, newPassword = "BrandNew123" }, _ct);
        var second = await factory.LoginAsync("users-firstlogin@test.com", "BrandNew123", _ct);
        client.DefaultRequestHeaders.Authorization = new("Bearer", second.AccessToken);
        var unblocked = await client.GetAsync("api/lookups/mass-types", _ct);

        Assert.True(first.User.IsPasswordChangeRequired);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains(ErrorCodes.AuthPasswordChangeRequired, await blocked.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.False(second.User.IsPasswordChangeRequired);
        Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);
    }

    [Fact]
    public async Task Me_AnyRole_ReadsAndUpdatesOwnNameAndPhone_Async()
    {
        var priest = await factory.CreateClientAsAsync("priest@test.com", _ct);

        var update = await priest.PutAsJsonAsync("api/auth/me", new { fullName = " Father Joseph ", phone = "0911222333" }, _ct);
        var me = (await priest.GetFromJsonAsync<UserDto>("api/auth/me", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("priest@test.com", me.Email);
        Assert.Equal("Father Joseph", me.FullName);
        Assert.Equal("0911222333", me.Phone);
        Assert.Equal(RoleNames.ParishPriest, me.RoleName);
    }

    [Fact]
    public async Task Me_Anonymous_Returns401_Async()
    {
        var response = await factory.CreateClient().GetAsync("api/auth/me", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_AsNonAdmin_Returns403_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await director.GetAsync("api/users", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

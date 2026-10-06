using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;

namespace Harmonia.API.Tests;

public class UsersEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Page(List<UserDto> Items, int TotalCount);

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
            new { email = "users-named@test.com", fullName = " Maria Nguyen ", password = HarmoniaApiFactory.Password, roleName = RoleNames.ChoirDirector },
            _ct);
        var id = (await created.Content.ReadFromJsonAsync<UserDto>(TestJson.Options, _ct))!.Id;
        var fetched = (await admin.GetFromJsonAsync<UserDto>($"api/users/{id}", TestJson.Options, _ct))!;
        var login = await factory.LoginAsync("users-named@test.com", cancellationToken: _ct);

        Assert.True(created.IsSuccessStatusCode);
        Assert.Equal("Maria Nguyen", fetched.FullName);
        Assert.Equal("Maria Nguyen", login.User.FullName);
    }

    [Fact]
    public async Task Create_WithoutFullName_Returns400_Async()
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);

        var response = await admin.PostAsJsonAsync(
            "api/users",
            new { email = "users-noname@test.com", password = HarmoniaApiFactory.Password, roleName = RoleNames.ChoirMember },
            _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_AsNonAdmin_Returns403_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await director.GetAsync("api/users", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

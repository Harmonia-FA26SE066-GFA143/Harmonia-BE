using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Harmonia.API.Tests;

/// <summary>Authentication, role checks, SignalR token handling and CORS, through the real pipeline.</summary>
public class SecurityPipelineTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private const string NegotiatePath = "hubs/notifications/negotiate?negotiateVersion=1";

    private static async Task<ErrorResponse> ErrorOfAsync(
        HttpResponseMessage response, CancellationToken cancellationToken = default) =>
        (await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, cancellationToken))!;

    private static string SignToken(string key, DateTime expires, string role = RoleNames.ChoirMember)
    {
        var token = new JwtSecurityToken(
            issuer: "harmonia-test",
            audience: "harmonia-test-clients",
            claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            notBefore: expires.AddHours(-1),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void Factory_NeverLoadsTheDevelopersEnvFile()
    {
        var contentRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        Assert.False(File.Exists(Path.Combine(contentRoot, ".env")));
        Assert.DoesNotContain("Harmonia.API", contentRoot);
    }

    // ---- Authentication ----

    [Fact]
    public async Task NoToken_Returns401WithErrorBody_Async()
    {
        var response = await factory.CreateClient().GetAsync("api/notifications", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthTokenInvalid, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task GarbageToken_Returns401_Async()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        var response = await client.GetAsync("api/notifications", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401TokenExpired_Async()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SignToken("test-signing-key-that-is-at-least-32-bytes-long!!", DateTime.Now.AddMinutes(-1)));

        var response = await client.GetAsync("api/notifications", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthTokenExpired, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task TokenSignedWithOtherKey_Returns401_Async()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", SignToken("attacker-key-attacker-key-attacker-key-123", DateTime.Now.AddMinutes(10)));

        var response = await client.GetAsync("api/notifications", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Roles ----

    [Theory]
    [InlineData("admin@test.com")]
    [InlineData("priest@test.com")]
    [InlineData("director@test.com")]
    public async Task MemberOnlyEndpoint_OtherRoles_Return403_Async(string email)
    {
        var client = await factory.CreateClientAsAsync(email, _ct);

        var response = await client.GetAsync("api/member-profiles/me", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberOnlyEndpoint_ChoirMember_PassesAuthorization_Async()
    {
        var client = await factory.CreateClientAsAsync("member@test.com", _ct);

        var response = await client.GetAsync("api/member-profiles/me", _ct);

        // No profile is seeded, so the service answers 404; what matters is that auth let it through.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- SignalR ----

    [Fact]
    public async Task Hub_WithoutToken_Returns401_Async()
    {
        var response = await factory.CreateClient().PostAsync(NegotiatePath, null, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hub_AcceptsTokenFromQueryString_Async()
    {
        var login = await factory.LoginAsync("member@test.com", cancellationToken: _ct);

        var response = await factory.CreateClient().PostAsync(
            $"{NegotiatePath}&access_token={Uri.EscapeDataString(login.AccessToken)}", null, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QueryStringToken_IgnoredOutsideHubs_Async()
    {
        // 03-security.md: the query-string fallback is scoped to /hubs only.
        var login = await factory.LoginAsync("member@test.com", cancellationToken: _ct);

        var response = await factory.CreateClient().GetAsync(
            $"api/notifications?access_token={Uri.EscapeDataString(login.AccessToken)}", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- CORS ----

    private async Task<HttpResponseMessage> PreflightAsync(string origin, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "api/notifications");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return await factory.CreateClient().SendAsync(request, cancellationToken);
    }

    [Fact]
    public async Task Cors_AllowedOrigin_GetsCredentialedAllow_Async()
    {
        var response = await PreflightAsync(HarmoniaApiFactory.AllowedOrigin, _ct);

        Assert.Equal(HarmoniaApiFactory.AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Cors_UnknownOrigin_IsNotAllowed_Async()
    {
        var response = await PreflightAsync("https://evil.example", _ct);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}

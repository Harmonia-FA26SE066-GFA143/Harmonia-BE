using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Harmonia.API.Tests;

public partial class AuthEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<ErrorResponse> ErrorOfAsync(
        HttpResponseMessage response, CancellationToken cancellationToken = default) =>
        (await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, cancellationToken))!;

    // ---- Login ----

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndUser_Async()
    {
        var response = await _client.PostAsJsonAsync(
            "api/auth/login", new { email = "director@test.com", password = HarmoniaApiFactory.Password }, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options, _ct))!;
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.False(string.IsNullOrEmpty(body.RefreshToken));
        Assert.DoesNotContain("passwordHash", await response.Content.ReadAsStringAsync(_ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_RefreshTokenStoredOnlyAsHash_Async()
    {
        var login = await factory.LoginAsync("member@test.com", cancellationToken: _ct);

        var storedRaw = await factory.WithDbAsync((db, ct) => db.RefreshTokens.AnyAsync(t => t.TokenHash == login.RefreshToken, ct), _ct);

        Assert.False(storedRaw);
    }

    [Theory]
    [InlineData("director@test.com", "WrongPass1")]
    [InlineData("nobody@test.com", HarmoniaApiFactory.Password)]
    public async Task Login_BadCredentials_Returns401WithSameCode_Async(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("api/auth/login", new { email, password }, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task Login_InactiveAccount_Returns403_Async()
    {
        await factory.AddUserAsync("inactive@test.com", isActive: false, cancellationToken: _ct);

        var response = await _client.PostAsJsonAsync(
            "api/auth/login", new { email = "inactive@test.com", password = HarmoniaApiFactory.Password }, _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthAccountInactive, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task Login_InvalidBody_Returns400WithFieldCodes_Async()
    {
        var response = await _client.PostAsJsonAsync("api/auth/login", new { email = "not-an-email", password = "" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ErrorOfAsync(response, _ct);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains(ErrorCodes.AuthEmailInvalidFormat, error.Errors!["email"]);
        Assert.Contains(ErrorCodes.AuthPasswordRequired, error.Errors["password"]);
    }

    [Fact]
    public async Task Login_MalformedJson_Returns400_Async()
    {
        var response = await _client.PostAsync(
            "api/auth/login", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await ErrorOfAsync(response, _ct)).Code);
    }

    // ---- Refresh / logout ----

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenIsRejected_Async()
    {
        var login = await factory.LoginAsync("member@test.com", cancellationToken: _ct);

        var first = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = login.RefreshToken }, _ct);
        var reuse = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = login.RefreshToken }, _ct);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var rotated = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options, _ct))!;
        Assert.NotEqual(login.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(ErrorCodes.AuthRefreshTokenRevoked, (await ErrorOfAsync(reuse, _ct)).Code);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401_Async()
    {
        var response = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = "made-up" }, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthRefreshTokenNotFound, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_Async()
    {
        var login = await factory.LoginAsync("member@test.com", cancellationToken: _ct);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var logout = await client.PostAsJsonAsync("api/auth/logout", new { refreshToken = login.RefreshToken }, _ct);
        var refresh = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = login.RefreshToken }, _ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAccessToken_Returns401_Async()
    {
        var response = await _client.PostAsJsonAsync("api/auth/logout", new { refreshToken = "x" }, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesEveryDevice_Async()
    {
        var user = await factory.AddUserAsync("logout-all@test.com", cancellationToken: _ct);
        var phone = await factory.LoginAsync(user.Email, cancellationToken: _ct);
        var laptop = await factory.LoginAsync(user.Email, cancellationToken: _ct);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", laptop.AccessToken);

        var response = await client.PostAsync("api/auth/logout-all", null, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var token in new[] { phone.RefreshToken, laptop.RefreshToken })
        {
            var refresh = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = token }, _ct);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
    }

    // ---- Change password ----

    [Fact]
    public async Task ChangePassword_Valid_RevokesSessionsAndNewPasswordWorks_Async()
    {
        var user = await factory.AddUserAsync("change@test.com", cancellationToken: _ct);
        var login = await factory.LoginAsync(user.Email, cancellationToken: _ct);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var change = await client.PostAsJsonAsync(
            "api/auth/change-password",
            new { currentPassword = HarmoniaApiFactory.Password, newPassword = "BrandNew123" }, _ct);
        var oldRefresh = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = login.RefreshToken }, _ct);
        var oldPassword = await _client.PostAsJsonAsync(
            "api/auth/login", new { email = user.Email, password = HarmoniaApiFactory.Password }, _ct);
        var newPassword = await _client.PostAsJsonAsync(
            "api/auth/login", new { email = user.Email, password = "BrandNew123" }, _ct);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_Returns400_Async()
    {
        var client = await factory.CreateClientAsAsync("member2@test.com", _ct);

        var response = await client.PostAsJsonAsync(
            "api/auth/change-password", new { currentPassword = "Wrong1234", newPassword = "BrandNew123" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthCurrentPasswordInvalid, (await ErrorOfAsync(response, _ct)).Code);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400WithCode_Async()
    {
        var client = await factory.CreateClientAsAsync("member2@test.com", _ct);

        var response = await client.PostAsJsonAsync(
            "api/auth/change-password", new { currentPassword = HarmoniaApiFactory.Password, newPassword = "short" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.AuthPasswordTooWeak, (await ErrorOfAsync(response, _ct)).Errors!["newPassword"]);
    }

    // ---- Forgot / reset password ----

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns204AndSendsNothing_Async()
    {
        var response = await _client.PostAsJsonAsync("api/auth/forgot-password", new { email = "ghost@test.com" }, _ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await factory.EmailSender.DidNotReceive()
            .SendAsync("ghost@test.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotThenReset_FullFlow_Async()
    {
        var user = await factory.AddUserAsync("reset@test.com", cancellationToken: _ct);
        var session = await factory.LoginAsync(user.Email, cancellationToken: _ct);

        var forgot = await _client.PostAsJsonAsync("api/auth/forgot-password", new { email = user.Email }, _ct);
        var token = ExtractResetToken(user.Email);
        var reset = await _client.PostAsJsonAsync("api/auth/reset-password", new { token, newPassword = "AfterReset1" }, _ct);
        var reuse = await _client.PostAsJsonAsync("api/auth/reset-password", new { token, newPassword = "AfterReset2" }, _ct);
        var login = await _client.PostAsJsonAsync("api/auth/login", new { email = user.Email, password = "AfterReset1" }, _ct);
        var oldSession = await _client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = session.RefreshToken }, _ct);

        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        Assert.Equal(ErrorCodes.AuthResetTokenUsed, (await ErrorOfAsync(reuse, _ct)).Code);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
    }

    [Fact]
    public async Task ForgotTwice_OnlyNewestLinkWorks_Async()
    {
        var user = await factory.AddUserAsync("twice@test.com", cancellationToken: _ct);

        await _client.PostAsJsonAsync("api/auth/forgot-password", new { email = user.Email }, _ct);
        var firstToken = ExtractResetToken(user.Email);
        await _client.PostAsJsonAsync("api/auth/forgot-password", new { email = user.Email }, _ct);
        var secondToken = ExtractResetToken(user.Email);

        var first = await _client.PostAsJsonAsync("api/auth/reset-password", new { token = firstToken, newPassword = "Newer1234" }, _ct);
        var second = await _client.PostAsJsonAsync("api/auth/reset-password", new { token = secondToken, newPassword = "Newer1234" }, _ct);

        Assert.Equal(ErrorCodes.AuthResetTokenInvalid, (await ErrorOfAsync(first, _ct)).Code);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task ResetToken_StoredOnlyAsHash_Async()
    {
        var user = await factory.AddUserAsync("hashcheck@test.com", cancellationToken: _ct);

        await _client.PostAsJsonAsync("api/auth/forgot-password", new { email = user.Email }, _ct);
        var token = ExtractResetToken(user.Email);

        Assert.False(await factory.WithDbAsync((db, ct) => db.PasswordResetTokens.AnyAsync(t => t.TokenHash == token, ct), _ct));
    }

    /// <summary>Pulls the raw token out of the newest reset email sent to <paramref name="email"/>.</summary>
    private string ExtractResetToken(string email)
    {
        var body = factory.EmailSender.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "SendAsync" && (string)c.GetArguments()[0]! == email)
            .Select(c => (string)c.GetArguments()[2]!)
            .Last();
        var encoded = ResetLinkToken().Match(body).Groups[1].Value;

        return Uri.UnescapeDataString(WebUtility.HtmlDecode(encoded));
    }

    [GeneratedRegex("token=([^\"&]+)")]
    private static partial Regex ResetLinkToken();
}

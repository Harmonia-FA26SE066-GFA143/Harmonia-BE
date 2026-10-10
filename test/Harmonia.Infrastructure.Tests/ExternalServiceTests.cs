using System.Net;
using System.Security.Claims;
using Harmonia.Application.Common.Models;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

public class ExternalServiceTests
{
    // ---- PasswordHasherService ----

    private readonly PasswordHasherService _hasher = new();

    [Fact]
    public void HashPassword_NeverStoresPlaintext()
    {
        var hash = _hasher.HashPassword(new User(), "Secret123");

        Assert.NotEqual("Secret123", hash);
        Assert.DoesNotContain("Secret123", hash);
    }

    [Fact]
    public void HashPassword_SaltsEachHash()
    {
        var user = new User();

        Assert.NotEqual(_hasher.HashPassword(user, "Secret123"), _hasher.HashPassword(user, "Secret123"));
    }

    [Fact]
    public void VerifyPassword_RightPassword_Passes()
    {
        var user = new User();
        user.PasswordHash = _hasher.HashPassword(user, "Secret123");

        Assert.True(_hasher.VerifyPassword(user, "Secret123"));
    }

    [Theory]
    [InlineData("secret123")]
    [InlineData("Secret1234")]
    [InlineData("")]
    public void VerifyPassword_WrongPassword_Fails(string attempt)
    {
        var user = new User();
        user.PasswordHash = _hasher.HashPassword(user, "Secret123");

        Assert.False(_hasher.VerifyPassword(user, attempt));
    }

    // ---- CurrentUserService ----

    private static CurrentUserService CurrentUserFor(HttpContext? context)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);
        return new CurrentUserService(accessor);
    }

    [Fact]
    public void CurrentUser_AuthenticatedRequest_ReadsClaims()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, "Admin")],
                authenticationType: "Bearer")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        var sut = CurrentUserFor(context);

        Assert.Equal(userId, sut.UserId);
        Assert.Equal("Admin", sut.RoleName);
        Assert.True(sut.IsAuthenticated);
        Assert.Equal("127.0.0.1", sut.IpAddress);
    }

    [Fact]
    public void CurrentUser_NoHttpContext_ReturnsNothing()
    {
        // Background work and EF design-time run without a request.
        var sut = CurrentUserFor(null);

        Assert.Null(sut.UserId);
        Assert.Null(sut.RoleName);
        Assert.False(sut.IsAuthenticated);
        Assert.Null(sut.IpAddress);
    }

    [Fact]
    public void CurrentUser_MalformedUserId_ReturnsNullInsteadOfThrowing()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], "Bearer")),
        };

        Assert.Null(CurrentUserFor(context).UserId);
    }
    // ---- GeminiRosterSuggestionGenerator ----

    private static readonly List<RosterSlotInput> Slots =
        [new() { SlotCode = "S1", SkillName = "Soprano", NeededCount = 1, Candidates = [new() { MemberCode = "M1" }] }];

    private Task<Result<List<RosterSlotPick>>> GenerateAsync(StubHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://gemini.test/") };
        var options = Options.Create(new GeminiOptions { ApiKey = "secret-key", Model = "test-model" });
        return new GeminiRosterSuggestionGenerator(client, options, NullLogger<GeminiRosterSuggestionGenerator>.Instance)
            .GenerateAsync(Slots, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Gemini_ValidResponse_ParsesPicksAndSendsKeyInHeader_Async()
    {
        const string body = """{"candidates":[{"content":{"parts":[{"text":"[{\"slotCode\":\"S1\",\"memberCodes\":[\"M1\"]}]"}]}}]}""";
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);

        var result = await GenerateAsync(handler);

        var pick = Assert.Single(result.Value!);
        Assert.Equal("S1", pick.SlotCode);
        Assert.Equal(["M1"], pick.MemberCodes);
        Assert.Equal("/v1beta/models/test-model:generateContent", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("secret-key", Assert.Single(handler.LastRequest.Headers.GetValues("x-goog-api-key")));
        Assert.DoesNotContain("secret-key", handler.LastRequest.RequestUri.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, """{"candidates":[]}""")]
    [InlineData(HttpStatusCode.OK, """{"candidates":[{"content":{"parts":[{"text":"not json"}]}}]}""")]
    public async Task Gemini_ErrorOrUnexpectedResponse_ReturnsExternalAiFailed_Async(HttpStatusCode status, string body)
    {
        var result = await GenerateAsync(new StubHttpMessageHandler(status, body));

        Assert.Equal(ErrorCodes.ExternalAiFailed, result.Code);
    }
}

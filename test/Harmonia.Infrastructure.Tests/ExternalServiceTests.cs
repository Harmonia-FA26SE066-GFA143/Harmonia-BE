using System.Net;
using System.Security.Claims;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
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

    // ---- LiturgicalCalendarApiClient ----

    private static Task<Result<List<CalendarDayResult>>> GetYearAsync(
        StubHttpMessageHandler handler, int year = 2026)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://litcal.test/") };
        return new LiturgicalCalendarApiClient(client, NullLogger<LiturgicalCalendarApiClient>.Instance)
            .GetYearAsync(year, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LiturgicalCalendar_KeepsHighestGradePerDate_SkipsVigilsAndOtherYears_Async()
    {
        const string body = """
            {"litcal":[
              {"name":"Saint Sylvester I, Pope","date":"2025-12-31T00:00:00+00:00","grade":2,"liturgical_season":"CHRISTMAS"},
              {"name":"Thursday of the 4th Week of Advent","date":"2026-12-24T00:00:00+00:00","grade":0,"liturgical_season":"ADVENT"},
              {"name":"Christmas Vigil Mass","date":"2026-12-24T00:00:00+00:00","grade":7,"liturgical_season":"CHRISTMAS","is_vigil_mass":true},
              {"name":"Christmas","date":"2026-12-25T00:00:00+00:00","grade":7,"liturgical_season":"CHRISTMAS"},
              {"name":"Saint Stephen, The First Martyr","date":"2026-12-26T00:00:00+00:00","grade":4,"liturgical_season":"CHRISTMAS"},
              {"name":"Good Friday","date":"2026-04-03T00:00:00+00:00","grade":7,"liturgical_season":"EASTER_TRIDUUM"},
              {"name":"Saint Ignatius of Antioch","date":"2026-10-17T00:00:00+00:00","grade":3,"liturgical_season":"ORDINARY_TIME"},
              {"name":"29th Sunday of Ordinary Time Vigil Mass","date":"2026-10-17T00:00:00+00:00","grade":5,"liturgical_season":"ORDINARY_TIME","is_vigil_mass":true}
            ]}
            """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);

        var result = await GetYearAsync(handler);

        Assert.Equal(
            [
                (new DateOnly(2026, 4, 3), "Good Friday", (string?)"Solemnity", (string?)"Lent"),
                (new DateOnly(2026, 10, 17), "Saint Ignatius of Antioch", "Obligatory Memorial", "OrdinaryTime"),
                (new DateOnly(2026, 12, 24), "Thursday of the 4th Week of Advent", null, "Advent"),
                (new DateOnly(2026, 12, 25), "Christmas", "Solemnity", "Christmas"),
                (new DateOnly(2026, 12, 26), "Saint Stephen, The First Martyr", "Feast", "Christmas"),
            ],
            result.Value!.Select(d => (d.Date, d.CelebrationName, d.Rank, d.SeasonName)));
        var request = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/v5/calendar/2026", request.AbsolutePath);
        Assert.Contains("year_type=CIVIL", request.Query);
        Assert.Contains("ascension=SUNDAY", request.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, """{"litcal":[]}""")]
    public async Task LiturgicalCalendar_ErrorOrEmptyResponse_ReturnsExternalCalendarFailed_Async(
        HttpStatusCode status, string body)
    {
        var result = await GetYearAsync(new StubHttpMessageHandler(status, body));

        Assert.Equal(ErrorCodes.ExternalCalendarFailed, result.Code);
    }
}

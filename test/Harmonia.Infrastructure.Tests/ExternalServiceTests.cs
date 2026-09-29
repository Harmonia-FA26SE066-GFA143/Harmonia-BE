using System.Net;
using System.Security.Claims;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Http;
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
}

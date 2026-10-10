using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Domain.Exceptions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class AuthServiceTests
{
    private const string WebUrl = "https://web.test/reset";
    private const string MobileUrl = "harmonia://reset";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IPasswordHasherService _passwordHasher = Substitute.For<IPasswordHasherService>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IGoogleTokenValidator _googleTokenValidator = Substitute.For<IGoogleTokenValidator>();
    private readonly AuthService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public AuthServiceTests()
    {
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>()).Returns(("access", DateTime.UtcNow.AddMinutes(30)));
        _jwtTokenService.GenerateRefreshToken().Returns("raw-token");
        _jwtTokenService.HashRefreshToken(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());

        _sut = new AuthService(
            _userRepository,
            _jwtTokenService,
            _passwordHasher,
            _emailSender,
            _googleTokenValidator,
            Options.Create(new PasswordResetOptions { WebUrl = WebUrl, MobileUrl = MobileUrl }),
            Substitute.For<IMapper>());
    }

    private static User NewUser(bool isActive = true) =>
        new() { Id = Guid.NewGuid(), Email = "member@test.com", PasswordHash = "old-hash", IsActive = isActive };

    // ---- Login ----

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials_Async()
    {
        var result = await _sut.LoginAsync(new LoginRequest { Email = "x@test.com", Password = "p" }, _ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.Code);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsSameCodeAsUnknownEmail_Async()
    {
        var user = NewUser();
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);
        _passwordHasher.VerifyPassword(user, "wrong").Returns(false);

        var result = await _sut.LoginAsync(new LoginRequest { Email = user.Email, Password = "wrong" }, _ct);

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.Code);
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task LoginAsync_InactiveAccount_ReturnsAccountInactive_Async()
    {
        var user = NewUser(isActive: false);
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);
        _passwordHasher.VerifyPassword(user, "pass").Returns(true);

        var result = await _sut.LoginAsync(new LoginRequest { Email = user.Email, Password = "pass" }, _ct);

        Assert.Equal(ErrorCodes.AuthAccountInactive, result.Code);
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_StoresHashAndReturnsRawToken_Async()
    {
        var user = NewUser();
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);
        _passwordHasher.VerifyPassword(user, "pass").Returns(true);

        var result = await _sut.LoginAsync(
            new LoginRequest { Email = user.Email, Password = "pass", DeviceId = "d1", Platform = DevicePlatform.Android },
            _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("access", result.Value!.AccessToken);
        Assert.Equal("raw-token", result.Value.RefreshToken);
        Assert.NotNull(user.LastLoginAt);
        await _userRepository.Received(1).AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.UserId == user.Id
                && t.TokenHash == "hash:raw-token"
                && t.DeviceId == "d1"
                && t.Platform == DevicePlatform.Android),
            _ct);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }

    // ---- Google login ----

    [Fact]
    public async Task LoginWithGoogleAsync_InvalidToken_ReturnsGoogleTokenInvalid_Async()
    {
        _googleTokenValidator.GetVerifiedEmailAsync("bad", _ct).Returns((string?)null);

        var result = await _sut.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "bad" }, _ct);

        Assert.Equal(ErrorCodes.AuthGoogleTokenInvalid, result.Code);
        await _userRepository.DidNotReceive().GetByEmailAsync(Arg.Any<string>(), _ct);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_EmailWithoutAccount_ReturnsInvalidCredentials_Async()
    {
        _googleTokenValidator.GetVerifiedEmailAsync("id-token", _ct).Returns("stranger@gmail.com");

        var result = await _sut.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, _ct);

        Assert.Equal(ErrorCodes.AuthInvalidCredentials, result.Code);
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), _ct);
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_InactiveAccount_ReturnsAccountInactive_Async()
    {
        var user = NewUser(isActive: false);
        _googleTokenValidator.GetVerifiedEmailAsync("id-token", _ct).Returns(user.Email);
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);

        var result = await _sut.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, _ct);

        Assert.Equal(ErrorCodes.AuthAccountInactive, result.Code);
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_KnownEmail_IssuesTokens_Async()
    {
        var user = NewUser();
        _googleTokenValidator.GetVerifiedEmailAsync("id-token", _ct).Returns(user.Email);
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);

        var result = await _sut.LoginWithGoogleAsync(
            new GoogleLoginRequest { IdToken = "id-token", DeviceId = "d1", Platform = DevicePlatform.Web }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("raw-token", result.Value!.RefreshToken);
        Assert.NotNull(user.LastLoginAt);
        await _userRepository.Received(1).AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.UserId == user.Id && t.DeviceId == "d1"), _ct);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }

    // ---- Refresh ----

    [Fact]
    public async Task RefreshTokenAsync_UnknownToken_ReturnsNotFound_Async()
    {
        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "nope" }, _ct);

        Assert.Equal(ErrorCodes.AuthRefreshTokenNotFound, result.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RevokesOldAndIssuesNew_Async()
    {
        var user = NewUser();
        var old = new RefreshToken
        {
            UserId = user.Id, User = user, TokenHash = "hash:old", ExpiresAt = DateTime.UtcNow.AddDays(1),
            DeviceId = "d1", Platform = DevicePlatform.iOS,
        };
        _userRepository.GetRefreshTokenByHashAsync("hash:old", _ct).Returns(old);

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "old" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(old.RevokedAt);
        await _userRepository.Received(1).AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.TokenHash == "hash:raw-token" && t.DeviceId == "d1" && t.Platform == DevicePlatform.iOS),
            _ct);
    }

    [Fact]
    public async Task RefreshTokenAsync_InactiveUser_RevokesAllAndIssuesNothing_Async()
    {
        var user = NewUser(isActive: false);
        var old = new RefreshToken
        {
            UserId = user.Id, User = user, TokenHash = "hash:old", ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
        _userRepository.GetRefreshTokenByHashAsync("hash:old", _ct).Returns(old);

        var result = await _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "old" }, _ct);

        Assert.Equal(ErrorCodes.AuthAccountInactive, result.Code);
        Assert.NotNull(old.RevokedAt);
        await _userRepository.Received(1).RevokeAllRefreshTokensAsync(user.Id, _ct);
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ThrowsAndIssuesNothing_Async()
    {
        var old = new RefreshToken
        {
            User = NewUser(), ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow.AddMinutes(-1),
        };
        _userRepository.GetRefreshTokenByHashAsync("hash:old", _ct).Returns(old);

        await Assert.ThrowsAsync<RefreshTokenRevokedException>(
            () => _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "old" }, _ct));
        await _userRepository.DidNotReceive().AddRefreshTokenAsync(Arg.Any<RefreshToken>(), _ct);
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_Throws_Async()
    {
        var old = new RefreshToken { User = NewUser(), ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };
        _userRepository.GetRefreshTokenByHashAsync("hash:old", _ct).Returns(old);

        await Assert.ThrowsAsync<RefreshTokenExpiredException>(
            () => _sut.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = "old" }, _ct));
    }

    // ---- Logout ----

    [Fact]
    public async Task LogoutAsync_UnknownToken_ReturnsNotFound_Async()
    {
        var result = await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "nope" }, _ct);

        Assert.Equal(ErrorCodes.AuthRefreshTokenNotFound, result.Code);
    }

    [Fact]
    public async Task LogoutAsync_ValidToken_MarksRevokedAt_Async()
    {
        var token = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _userRepository.GetRefreshTokenByHashAsync("hash:t", _ct).Returns(token);

        var result = await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "t" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(token.RevokedAt);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task LogoutAllAsync_RevokesEveryTokenOfUser_Async()
    {
        var userId = Guid.NewGuid();

        var result = await _sut.LogoutAllAsync(userId, _ct);

        Assert.True(result.IsSuccess);
        await _userRepository.Received(1).RevokeAllRefreshTokensAsync(userId, _ct);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }

    // ---- Change password ----

    [Fact]
    public async Task ChangePasswordAsync_UnknownUser_ReturnsUserNotFound_Async()
    {
        var result = await _sut.ChangePasswordAsync(
            Guid.NewGuid(), new ChangePasswordRequest { CurrentPassword = "a", NewPassword = "b" }, _ct);

        Assert.Equal(ErrorCodes.UserNotFound, result.Code);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_KeepsHash_Async()
    {
        var user = NewUser();
        _userRepository.GetByIdAsync(user.Id, _ct).Returns(user);
        _passwordHasher.VerifyPassword(user, "wrong").Returns(false);

        var result = await _sut.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest { CurrentPassword = "wrong", NewPassword = "NewPass123" }, _ct);

        Assert.Equal(ErrorCodes.AuthCurrentPasswordInvalid, result.Code);
        Assert.Equal("old-hash", user.PasswordHash);
        await _userRepository.DidNotReceive().RevokeAllRefreshTokensAsync(Arg.Any<Guid>(), _ct);
    }

    [Fact]
    public async Task ChangePasswordAsync_Valid_UpdatesHashAndRevokesAllTokens_Async()
    {
        var user = NewUser();
        _userRepository.GetByIdAsync(user.Id, _ct).Returns(user);
        _passwordHasher.VerifyPassword(user, "OldPass123").Returns(true);
        _passwordHasher.HashPassword(user, "NewPass123").Returns("new-hash");

        var result = await _sut.ChangePasswordAsync(
            user.Id, new ChangePasswordRequest { CurrentPassword = "OldPass123", NewPassword = "NewPass123" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-hash", user.PasswordHash);
        await _userRepository.Received(1).RevokeAllRefreshTokensAsync(user.Id, _ct);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }

    // ---- Forgot password ----

    [Fact]
    public async Task ForgotPasswordAsync_UnknownEmail_SucceedsWithoutSendingEmail_Async()
    {
        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "x@test.com" }, _ct);

        Assert.True(result.IsSuccess);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, _ct);
        await _userRepository.DidNotReceiveWithAnyArgs().AddPasswordResetTokenAsync(default!, _ct);
    }

    [Fact]
    public async Task ForgotPasswordAsync_InactiveUser_SucceedsWithoutSendingEmail_Async()
    {
        var user = NewUser(isActive: false);
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email }, _ct);

        Assert.True(result.IsSuccess);
        await _emailSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, _ct);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ActiveUser_RemovesOldTokensAndStoresHashWithOneHourExpiry_Async()
    {
        var user = NewUser();
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email }, _ct);

        Assert.True(result.IsSuccess);
        Received.InOrder(() =>
        {
            _userRepository.RemoveUnusedPasswordResetTokensAsync(user.Id, _ct);
            _userRepository.AddPasswordResetTokenAsync(Arg.Any<PasswordResetToken>(), _ct);
        });
        await _userRepository.Received(1).AddPasswordResetTokenAsync(
            Arg.Is<PasswordResetToken>(t => t.UserId == user.Id
                && t.TokenHash == "hash:raw-token"
                && t.ExpiresAt > DateTime.UtcNow.AddMinutes(59)
                && t.ExpiresAt <= DateTime.UtcNow.AddHours(1)),
            _ct);
    }

    [Theory]
    [InlineData(null, WebUrl)]
    [InlineData(DevicePlatform.Web, WebUrl)]
    [InlineData(DevicePlatform.Android, MobileUrl)]
    [InlineData(DevicePlatform.iOS, MobileUrl)]
    public async Task ForgotPasswordAsync_PicksLinkByPlatform_Async(DevicePlatform? platform, string expectedBaseUrl)
    {
        var user = NewUser();
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);

        await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email, Platform = platform }, _ct);

        await _emailSender.Received(1).SendAsync(
            user.Email,
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains($"{expectedBaseUrl}?token=raw-token")),
            _ct);
    }

    [Fact]
    public async Task ForgotPasswordAsync_EmailDeliveryFails_StillSucceeds_Async()
    {
        var user = NewUser();
        _userRepository.GetByEmailAsync(user.Email, _ct).Returns(user);
        _emailSender.SendAsync(default!, default!, default!, _ct)
            .ReturnsForAnyArgs(Result.Failure("EXTERNAL_EMAIL_FAILED"));

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email }, _ct);

        Assert.True(result.IsSuccess);
    }

    // ---- Reset password ----

    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_ReturnsInvalid_Async()
    {
        var result = await _sut.ResetPasswordAsync(
            new ResetPasswordRequest { Token = "nope", NewPassword = "NewPass123" }, _ct);

        Assert.Equal(ErrorCodes.AuthResetTokenInvalid, result.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_InactiveUser_ReturnsInvalid_Async()
    {
        var token = new PasswordResetToken { User = NewUser(isActive: false), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _userRepository.GetPasswordResetTokenByHashAsync("hash:t", _ct).Returns(token);

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "t", NewPassword = "NewPass123" }, _ct);

        Assert.Equal(ErrorCodes.AuthResetTokenInvalid, result.Code);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_UsedToken_Throws_Async()
    {
        var token = new PasswordResetToken
        {
            User = NewUser(), ExpiresAt = DateTime.UtcNow.AddHours(1), UsedAt = DateTime.UtcNow.AddMinutes(-5),
        };
        _userRepository.GetPasswordResetTokenByHashAsync("hash:t", _ct).Returns(token);

        await Assert.ThrowsAsync<PasswordResetTokenUsedException>(
            () => _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "t", NewPassword = "NewPass123" }, _ct));
        await _userRepository.DidNotReceive().RevokeAllRefreshTokensAsync(Arg.Any<Guid>(), _ct);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_Throws_Async()
    {
        var token = new PasswordResetToken { User = NewUser(), ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };
        _userRepository.GetPasswordResetTokenByHashAsync("hash:t", _ct).Returns(token);

        await Assert.ThrowsAsync<PasswordResetTokenExpiredException>(
            () => _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "t", NewPassword = "NewPass123" }, _ct));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_MarksUsedUpdatesHashAndRevokesAllTokens_Async()
    {
        var user = NewUser();
        var token = new PasswordResetToken { UserId = user.Id, User = user, ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _userRepository.GetPasswordResetTokenByHashAsync("hash:t", _ct).Returns(token);
        _passwordHasher.HashPassword(user, "NewPass123").Returns("new-hash");

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "t", NewPassword = "NewPass123" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(token.UsedAt);
        Assert.Equal("new-hash", user.PasswordHash);
        await _userRepository.Received(1).RevokeAllRefreshTokensAsync(user.Id, _ct);
        await _userRepository.Received(1).SaveChangesAsync(_ct);
    }
}

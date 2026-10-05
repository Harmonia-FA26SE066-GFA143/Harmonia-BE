using FluentValidation;
using Harmonia.Application.DTOs;
using Harmonia.Application.Validators;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Tests;

public class ValidatorTests
{
    private static string[] ErrorCodesOf<T>(IValidator<T> validator, T request) =>
        validator.Validate(request).Errors.Select(e => e.ErrorCode).ToArray();

    // ---- Login ----

    [Fact]
    public void Login_Valid_HasNoErrors()
    {
        var codes = ErrorCodesOf(new LoginRequestValidator(), new LoginRequest { Email = "a@test.com", Password = "x" });

        Assert.Empty(codes);
    }

    [Theory]
    [InlineData("", "x", ErrorCodes.AuthEmailRequired)]
    [InlineData("not-an-email", "x", ErrorCodes.AuthEmailInvalidFormat)]
    [InlineData("a@test.com", "", ErrorCodes.AuthPasswordRequired)]
    public void Login_Invalid_ReturnsCode(string email, string password, string expectedCode)
    {
        var codes = ErrorCodesOf(new LoginRequestValidator(), new LoginRequest { Email = email, Password = password });

        Assert.Contains(expectedCode, codes);
    }

    // ---- Refresh / Logout ----

    [Fact]
    public void RefreshToken_Empty_ReturnsRequired()
    {
        var codes = ErrorCodesOf(new RefreshTokenRequestValidator(), new RefreshTokenRequest());

        Assert.Equal([ErrorCodes.AuthRefreshTokenRequired], codes);
    }

    [Fact]
    public void Logout_Empty_ReturnsRequired()
    {
        var codes = ErrorCodesOf(new LogoutRequestValidator(), new LogoutRequest());

        Assert.Equal([ErrorCodes.AuthRefreshTokenRequired], codes);
    }

    // ---- Forgot password ----

    [Theory]
    [InlineData(null)]
    [InlineData(DevicePlatform.Web)]
    [InlineData(DevicePlatform.Android)]
    public void ForgotPassword_Valid_HasNoErrors(DevicePlatform? platform)
    {
        var codes = ErrorCodesOf(
            new ForgotPasswordRequestValidator(), new ForgotPasswordRequest { Email = "a@test.com", Platform = platform });

        Assert.Empty(codes);
    }

    [Fact]
    public void ForgotPassword_UnknownPlatform_ReturnsValidationFailed()
    {
        var codes = ErrorCodesOf(
            new ForgotPasswordRequestValidator(),
            new ForgotPasswordRequest { Email = "a@test.com", Platform = (DevicePlatform)99 });

        Assert.Equal([ErrorCodes.ValidationFailed], codes);
    }

    [Fact]
    public void ForgotPassword_BadEmail_ReturnsInvalidFormat()
    {
        var codes = ErrorCodesOf(new ForgotPasswordRequestValidator(), new ForgotPasswordRequest { Email = "abc" });

        Assert.Equal([ErrorCodes.AuthEmailInvalidFormat], codes);
    }

    // ---- Strong password (shared by change / reset) ----

    [Theory]
    [InlineData("", ErrorCodes.AuthPasswordRequired)]
    [InlineData("abc123", ErrorCodes.AuthPasswordTooWeak)]      // too short
    [InlineData("abcdefgh", ErrorCodes.AuthPasswordTooWeak)]    // no digit
    [InlineData("12345678", ErrorCodes.AuthPasswordTooWeak)]    // no letter
    public void ChangePassword_WeakNewPassword_ReturnsSingleCode(string newPassword, string expectedCode)
    {
        var codes = ErrorCodesOf(
            new ChangePasswordRequestValidator(),
            new ChangePasswordRequest { CurrentPassword = "Old12345", NewPassword = newPassword });

        // Cascade.Stop: an empty password must not also report TOO_WEAK.
        Assert.Equal([expectedCode], codes);
    }

    [Fact]
    public void ChangePassword_Valid_HasNoErrors()
    {
        var codes = ErrorCodesOf(
            new ChangePasswordRequestValidator(),
            new ChangePasswordRequest { CurrentPassword = "Old12345", NewPassword = "abcd1234" });

        Assert.Empty(codes);
    }

    [Fact]
    public void ChangePassword_EmptyCurrent_ReturnsRequired()
    {
        var codes = ErrorCodesOf(
            new ChangePasswordRequestValidator(), new ChangePasswordRequest { NewPassword = "abcd1234" });

        Assert.Equal([ErrorCodes.AuthPasswordRequired], codes);
    }

    [Theory]
    [InlineData("", "abcd1234", ErrorCodes.AuthResetTokenInvalid)]
    [InlineData("token", "short1", ErrorCodes.AuthPasswordTooWeak)]
    [InlineData("token", "", ErrorCodes.AuthPasswordRequired)]
    public void ResetPassword_Invalid_ReturnsSingleCode(string token, string newPassword, string expectedCode)
    {
        var codes = ErrorCodesOf(
            new ResetPasswordRequestValidator(), new ResetPasswordRequest { Token = token, NewPassword = newPassword });

        Assert.Equal([expectedCode], codes);
    }

    [Fact]
    public void ResetPassword_Valid_HasNoErrors()
    {
        var codes = ErrorCodesOf(
            new ResetPasswordRequestValidator(), new ResetPasswordRequest { Token = "token", NewPassword = "abcd1234" });

        Assert.Empty(codes);
    }

    // ---- Song classification ----

    [Fact]
    public void SongClassification_DuplicateTargetId_ReturnsClassificationDuplicate()
    {
        var id = Guid.NewGuid();
        var codes = ErrorCodesOf(new UpdateSongClassificationRequestValidator(),
            new UpdateSongClassificationRequest { SongThemeIds = [id, id] });

        Assert.Contains(ErrorCodes.SongClassificationDuplicate, codes);
    }

    [Fact]
    public void SongClassification_DuplicateSkill_ReturnsSkillRequirementDuplicate()
    {
        var id = Guid.NewGuid();
        var codes = ErrorCodesOf(new UpdateSongClassificationRequestValidator(),
            new UpdateSongClassificationRequest { VocalRequirements = [new() { SkillId = id }, new() { SkillId = id, IsMandatory = true }] });

        Assert.Contains(ErrorCodes.SongSkillRequirementDuplicate, codes);
    }

    [Fact]
    public void SongClassification_NullList_ReturnsErrorWithoutThrowing()
    {
        var codes = ErrorCodesOf(new UpdateSongClassificationRequestValidator(),
            new UpdateSongClassificationRequest { MassTypeIds = null!, InstrumentRequirements = null! });

        Assert.Equal(2, codes.Length);
    }

    // ---- Material learning progress ----

    [Theory]
    [InlineData(LearningStatus.Learned)]
    [InlineData(LearningStatus.NeedsPractice)]
    public void LearningProgress_LearnedOrNeedsPractice_HasNoErrors(LearningStatus status)
    {
        var codes = ErrorCodesOf(new UpdateMaterialLearningProgressRequestValidator(),
            new UpdateMaterialLearningProgressRequest { Status = status });

        Assert.Empty(codes);
    }

    [Theory]
    [InlineData(LearningStatus.NotStarted)]
    [InlineData((LearningStatus)99)]
    public void LearningProgress_OtherStatus_ReturnsStatusInvalid(LearningStatus status)
    {
        var codes = ErrorCodesOf(new UpdateMaterialLearningProgressRequestValidator(),
            new UpdateMaterialLearningProgressRequest { Status = status });

        Assert.Equal([ErrorCodes.MaterialLearningStatusInvalid], codes);
    }

    // ---- DeclareMemberSkill ----

    [Theory]
    [InlineData(null)]
    [InlineData(SkillLevel.Advanced)]
    public void DeclareMemberSkill_Valid_HasNoErrors(SkillLevel? level)
    {
        var result = new DeclareMemberSkillRequestValidator().Validate(
            new DeclareMemberSkillRequest { SkillId = Guid.NewGuid(), Level = level });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DeclareMemberSkill_EmptySkillId_IsInvalid()
    {
        var result = new DeclareMemberSkillRequestValidator().Validate(new DeclareMemberSkillRequest());

        Assert.Equal(nameof(DeclareMemberSkillRequest.SkillId), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void DeclareMemberSkill_UnknownLevel_IsInvalid()
    {
        var result = new DeclareMemberSkillRequestValidator().Validate(
            new DeclareMemberSkillRequest { SkillId = Guid.NewGuid(), Level = (SkillLevel)99 });

        Assert.Equal(nameof(DeclareMemberSkillRequest.Level), Assert.Single(result.Errors).PropertyName);
    }
}

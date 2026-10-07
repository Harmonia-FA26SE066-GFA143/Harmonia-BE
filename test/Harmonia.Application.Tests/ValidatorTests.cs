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

    // ---- Song personnel requirements ----

    [Fact]
    public void PersonnelRequirements_DuplicateSkill_ReturnsRequirementDuplicate()
    {
        var id = Guid.NewGuid();
        var codes = ErrorCodesOf(new UpdateSongPersonnelRequirementsRequestValidator(),
            new UpdateSongPersonnelRequirementsRequest { Requirements = [new() { SkillId = id, RequiredCount = 1 }, new() { SkillId = id, RequiredCount = 2 }] });

        Assert.Equal([ErrorCodes.PersonnelRequirementDuplicate], codes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(UpdateSongPersonnelRequirementsRequestValidator.MaxRequiredCount + 1)]
    public void PersonnelRequirements_CountOutOfRange_ReturnsRequiredCountInvalid(int count)
    {
        var codes = ErrorCodesOf(new UpdateSongPersonnelRequirementsRequestValidator(),
            new UpdateSongPersonnelRequirementsRequest { Requirements = [new() { SkillId = Guid.NewGuid(), RequiredCount = count }] });

        Assert.Equal([ErrorCodes.PersonnelRequiredCountInvalid], codes);
    }

    [Fact]
    public void PersonnelRequirements_Valid_HasNoErrors()
    {
        var codes = ErrorCodesOf(new UpdateSongPersonnelRequirementsRequestValidator(),
            new UpdateSongPersonnelRequirementsRequest { Requirements = [new() { SkillId = Guid.NewGuid(), RequiredCount = UpdateSongPersonnelRequirementsRequestValidator.MaxRequiredCount }] });

        Assert.Empty(codes);
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

    // ---- Practice assignment ----

    private static CreatePracticeAssignmentRequest PracticeAssignment(AssignmentScope scope) => new()
    {
        Title = "Learn the entrance hymn",
        Scope = scope,
        DueDate = DateTime.UtcNow.AddDays(3),
    };

    [Fact]
    public void CreatePracticeAssignment_AllScopeWithoutTargets_HasNoErrors()
    {
        var codes = ErrorCodesOf(new CreatePracticeAssignmentRequestValidator(), PracticeAssignment(AssignmentScope.All));

        Assert.Empty(codes);
    }

    [Fact]
    public void CreatePracticeAssignment_DueDateInPast_ReturnsDueDateInPast()
    {
        var request = PracticeAssignment(AssignmentScope.All);
        request.DueDate = DateTime.UtcNow.AddMinutes(-1);

        var codes = ErrorCodesOf(new CreatePracticeAssignmentRequestValidator(), request);

        Assert.Equal([ErrorCodes.PracticeDueDateInPast], codes);
    }

    [Theory]
    [InlineData(AssignmentScope.SkillGroup)]
    [InlineData(AssignmentScope.Individual)]
    public void CreatePracticeAssignment_TargetedScopeWithoutTargets_ReturnsTargetRequired(AssignmentScope scope)
    {
        var codes = ErrorCodesOf(new CreatePracticeAssignmentRequestValidator(), PracticeAssignment(scope));

        Assert.Equal([ErrorCodes.PracticeTargetRequired], codes);
    }

    // ---- Practice review ----

    [Theory]
    [InlineData(SubmissionStatus.Passed, null, true)]
    [InlineData(SubmissionStatus.Passed, "Well done", true)]
    [InlineData(SubmissionStatus.NeedsRevision, "Hold the last note", true)]
    [InlineData(SubmissionStatus.NeedsRevision, null, false)]
    [InlineData(SubmissionStatus.NeedsRevision, "   ", false)]
    [InlineData(SubmissionStatus.Submitted, null, false)]
    [InlineData(SubmissionStatus.Overdue, null, false)]
    public void ReviewPracticeSubmission_ResultAndComment(SubmissionStatus result, string? comment, bool isValid)
    {
        var validation = new ReviewPracticeSubmissionRequestValidator().Validate(
            new ReviewPracticeSubmissionRequest { Result = result, Comment = comment });

        Assert.Equal(isValid, validation.IsValid);
    }

    [Fact]
    public void ReviewPracticeSubmission_CommentTooLong_IsInvalid()
    {
        var validation = new ReviewPracticeSubmissionRequestValidator().Validate(
            new ReviewPracticeSubmissionRequest { Result = SubmissionStatus.Passed, Comment = new string('a', 1001) });

        Assert.False(validation.IsValid);
    }

    // ---- Practice submission ----

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(3600, true)]
    [InlineData(0, false)]
    [InlineData(3601, false)]
    public void CreatePracticeSubmission_DurationSeconds_IsValidOnlyInRange(int? durationSeconds, bool isValid)
    {
        var result = new CreatePracticeSubmissionRequestValidator().Validate(
            new CreatePracticeSubmissionRequest { DurationSeconds = durationSeconds });

        Assert.Equal(isValid, result.IsValid);
    }
}

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

    [Fact]
    public void Login_DeviceIdTooLong_HasError()
    {
        var request = new LoginRequest { Email = "a@test.com", Password = "x", DeviceId = new string('d', 101) };

        Assert.NotEmpty(ErrorCodesOf(new LoginRequestValidator(), request));
    }

    [Fact]
    public void GoogleLogin_DeviceIdTooLong_HasError()
    {
        var request = new GoogleLoginRequest { IdToken = "token", DeviceId = new string('d', 101) };

        Assert.NotEmpty(ErrorCodesOf(new GoogleLoginRequestValidator(), request));
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
        ChoirId = Guid.NewGuid(),
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

    // ---- Practice extra feedback ----

    [Theory]
    [InlineData("Nice", null, true)]
    [InlineData("Nice", SubmissionStatus.Passed, true)]
    [InlineData("Redo bar 4", SubmissionStatus.NeedsRevision, true)]
    [InlineData("", null, false)]
    [InlineData("   ", SubmissionStatus.Passed, false)]
    [InlineData("Nice", SubmissionStatus.Submitted, false)]
    [InlineData("Nice", SubmissionStatus.Overdue, false)]
    public void AddPracticeFeedback_CommentAndResult(string comment, SubmissionStatus? result, bool isValid)
    {
        var validation = new AddPracticeFeedbackRequestValidator().Validate(
            new AddPracticeFeedbackRequest { Comment = comment, Result = result });

        Assert.Equal(isValid, validation.IsValid);
    }

    [Fact]
    public void AddPracticeFeedback_CommentTooLong_IsInvalid()
    {
        var validation = new AddPracticeFeedbackRequestValidator().Validate(
            new AddPracticeFeedbackRequest { Comment = new string('a', 1001) });

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

    // ---- Rehearsal attendance ----

    [Fact]
    public void RecordAttendances_DuplicateMember_ReturnsDuplicate()
    {
        var id = Guid.NewGuid();
        var codes = ErrorCodesOf(new RecordRehearsalAttendancesRequestValidator(),
            new RecordRehearsalAttendancesRequest { Items = [new() { MemberId = id, Status = AttendanceStatus.Present }, new() { MemberId = id, Status = AttendanceStatus.Late }] });

        Assert.Equal([ErrorCodes.RehearsalAttendanceMemberDuplicate], codes);
    }

    [Fact]
    public void RecordAttendances_EmptyOrUnknownStatus_IsInvalid()
    {
        var validator = new RecordRehearsalAttendancesRequestValidator();

        Assert.False(validator.Validate(new RecordRehearsalAttendancesRequest()).IsValid);
        Assert.False(validator.Validate(new RecordRehearsalAttendancesRequest
        {
            Items = [new() { MemberId = Guid.NewGuid(), Status = (AttendanceStatus)99 }]
        }).IsValid);
    }

    // ---- Director note ----

    private static CreateDirectorNoteRequest ValidNote() =>
        new() { EventId = Guid.NewGuid(), ToUserIds = [Guid.NewGuid()], Content = "Note" };

    [Fact]
    public void DirectorNote_Valid_HasNoErrors()
    {
        Assert.Empty(ErrorCodesOf(new CreateDirectorNoteRequestValidator(), ValidNote()));
        var byDate = ValidNote();
        byDate.EventId = null;
        byDate.NoteDate = new DateOnly(2026, 12, 24);
        Assert.Empty(ErrorCodesOf(new CreateDirectorNoteRequestValidator(), byDate));
    }

    [Fact]
    public void DirectorNote_NoDateNorEvent_ReturnsTargetRequired()
    {
        var request = ValidNote();
        request.EventId = null;

        Assert.Equal([ErrorCodes.DirectorNoteTargetRequired], ErrorCodesOf(new CreateDirectorNoteRequestValidator(), request));
    }

    [Fact]
    public void DirectorNote_NoRecipient_ReturnsRecipientInvalid()
    {
        var request = ValidNote();
        request.ToUserIds = [];

        Assert.Equal([ErrorCodes.DirectorNoteRecipientInvalid], ErrorCodesOf(new CreateDirectorNoteRequestValidator(), request));
    }

    [Fact]
    public void DirectorNote_EmptyOrTooLongContent_IsInvalid()
    {
        var validator = new CreateDirectorNoteRequestValidator();
        var empty = ValidNote();
        empty.Content = " ";
        var tooLong = ValidNote();
        tooLong.Content = new string('a', 2001);

        Assert.False(validator.Validate(empty).IsValid);
        Assert.False(validator.Validate(tooLong).IsValid);
    }

    // ---- Song list ----

    private static UpdateSongListItemRequest SongListItem(Guid? slotId = null, Guid? songId = null) =>
        new() { SongId = songId ?? Guid.NewGuid(), SlotId = slotId ?? Guid.NewGuid(), DisplayOrder = 1 };

    [Fact]
    public void CreateSongList_Valid_HasNoErrors()
    {
        var request = new CreateSongListRequest { EventId = Guid.NewGuid(), Items = [SongListItem(), SongListItem()] };

        Assert.Empty(ErrorCodesOf(new CreateSongListRequestValidator(), request));
    }

    [Fact]
    public void CreateSongList_NoItems_ReturnsEmpty()
    {
        var request = new CreateSongListRequest { EventId = Guid.NewGuid() };

        Assert.Contains(ErrorCodes.SongListEmpty, ErrorCodesOf(new CreateSongListRequestValidator(), request));
    }

    [Fact]
    public void CreateSongList_SeveralSongsInOneSlot_HasNoErrors()
    {
        var slotId = Guid.NewGuid();
        var request = new CreateSongListRequest { EventId = Guid.NewGuid(), Items = [SongListItem(slotId), SongListItem(slotId)] };

        Assert.Empty(ErrorCodesOf(new CreateSongListRequestValidator(), request));
    }

    [Fact]
    public void CreateSongList_SameSongTwiceInOneSlot_ReturnsSlotDuplicate()
    {
        var (slotId, songId) = (Guid.NewGuid(), Guid.NewGuid());
        var request = new CreateSongListRequest
        {
            EventId = Guid.NewGuid(), Items = [SongListItem(slotId, songId), SongListItem(slotId, songId)],
        };

        Assert.Equal([ErrorCodes.SongListSlotDuplicate], ErrorCodesOf(new CreateSongListRequestValidator(), request));
    }

    [Fact]
    public void CreateSongList_SameSongInDifferentSlots_HasNoErrors()
    {
        var songId = Guid.NewGuid();
        var request = new CreateSongListRequest
        {
            EventId = Guid.NewGuid(), Items = [SongListItem(songId: songId), SongListItem(songId: songId)],
        };

        Assert.Empty(ErrorCodesOf(new CreateSongListRequestValidator(), request));
    }

    [Fact]
    public void UpdateSongListItems_NoItemsOrSameSongTwiceInOneSlot_ReturnsCode()
    {
        var (slotId, songId) = (Guid.NewGuid(), Guid.NewGuid());
        var validator = new UpdateSongListItemsRequestValidator();

        Assert.Contains(ErrorCodes.SongListEmpty, ErrorCodesOf(validator, new UpdateSongListItemsRequest()));
        Assert.Equal(
            [ErrorCodes.SongListSlotDuplicate],
            ErrorCodesOf(validator, new UpdateSongListItemsRequest
            {
                Items = [SongListItem(slotId, songId), SongListItem(slotId, songId)],
            }));
        Assert.Empty(ErrorCodesOf(validator, new UpdateSongListItemsRequest { Items = [SongListItem(slotId), SongListItem(slotId)] }));
    }

    [Theory]
    [InlineData(ReviewDecision.Reject)]
    [InlineData(ReviewDecision.RequestRevision)]
    public void ReviewSongList_RejectOrRevisionWithoutNotes_ReturnsNotesRequired(ReviewDecision decision)
    {
        var codes = ErrorCodesOf(new ReviewSongListRequestValidator(), new ReviewSongListRequest { Decision = decision });

        Assert.Equal([ErrorCodes.ReviewNotesRequired], codes);
    }

    [Fact]
    public void ReviewSongList_ApproveWithoutNotes_HasNoErrors()
    {
        var codes = ErrorCodesOf(new ReviewSongListRequestValidator(), new ReviewSongListRequest { Decision = ReviewDecision.Approve });

        Assert.Empty(codes);
    }

    // ---- Rehearsal songs ----

    [Fact]
    public void UpdateRehearsalSongs_ValidOrEmpty_HasNoErrors()
    {
        var validator = new UpdateRehearsalSongsRequestValidator();
        var request = new UpdateRehearsalSongsRequest
        {
            Items = [new() { SongId = Guid.NewGuid(), Note = "slowly" }, new() { SongId = Guid.NewGuid() }],
        };

        Assert.Empty(ErrorCodesOf(validator, request));
        Assert.Empty(ErrorCodesOf(validator, new UpdateRehearsalSongsRequest()));
    }

    [Fact]
    public void UpdateRehearsalSongs_SameSongTwice_ReturnsSongDuplicate()
    {
        var songId = Guid.NewGuid();
        var request = new UpdateRehearsalSongsRequest { Items = [new() { SongId = songId }, new() { SongId = songId }] };

        Assert.Equal([ErrorCodes.RehearsalSongDuplicate], ErrorCodesOf(new UpdateRehearsalSongsRequestValidator(), request));
    }

    [Fact]
    public void UpdateRehearsalSongs_EmptySongIdOrLongNote_IsInvalid()
    {
        var validator = new UpdateRehearsalSongsRequestValidator();

        Assert.False(validator.Validate(new UpdateRehearsalSongsRequest { Items = [new()] }).IsValid);
        Assert.False(validator.Validate(new UpdateRehearsalSongsRequest
        {
            Items = [new() { SongId = Guid.NewGuid(), Note = new string('a', 501) }],
        }).IsValid);
    }

    // ---- Reports ----

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-10-01", null)]
    [InlineData("2026-10-01", "2026-10-01")]
    public void SearchReports_OpenOrOrderedRange_HasNoErrors(string? from, string? to)
    {
        var request = new SearchReportsRequest
        {
            FromDate = from is null ? null : DateOnly.Parse(from), ToDate = to is null ? null : DateOnly.Parse(to),
        };

        Assert.Empty(ErrorCodesOf(new SearchReportsRequestValidator(), request));
    }

    [Fact]
    public void SearchReports_FromDateAfterToDate_ReturnsDateRangeInvalid()
    {
        var request = new SearchReportsRequest { FromDate = new DateOnly(2026, 10, 2), ToDate = new DateOnly(2026, 10, 1) };

        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(new SearchReportsRequestValidator(), request));
    }

    [Fact]
    public void SaveLiturgicalSlotAndWorshipLocation_RejectBadShapes_AndAcceptGoodOnes()
    {
        var slots = new SaveLiturgicalSlotRequestValidator();
        var locations = new SaveWorshipLocationRequestValidator();

        Assert.True(slots.Validate(new SaveLiturgicalSlotRequest { Name = "Entrance", DefaultOrder = 0 }).IsValid);
        Assert.False(slots.Validate(new SaveLiturgicalSlotRequest { Name = "Entrance", DefaultOrder = -1 }).IsValid);
        Assert.False(slots.Validate(new SaveLiturgicalSlotRequest { Name = " " }).IsValid);
        Assert.True(locations.Validate(new SaveWorshipLocationRequest { Name = "Chapel" }).IsValid);
        Assert.False(locations.Validate(new SaveWorshipLocationRequest { Name = "" }).IsValid);
        Assert.False(locations.Validate(new SaveWorshipLocationRequest { Name = "Chapel", Address = new string('a', 301) }).IsValid);
    }

    [Fact]
    public void UpdateSystemSetting_AllowsAnEmptyValue_ButNotNullOrOverlongOnes()
    {
        var validator = new UpdateSystemSettingRequestValidator();

        Assert.True(validator.Validate(new UpdateSystemSettingRequest { Value = string.Empty }).IsValid);
        Assert.False(validator.Validate(new UpdateSystemSettingRequest { Value = null! }).IsValid);
        Assert.False(validator.Validate(new UpdateSystemSettingRequest { Value = new string('a', 1001) }).IsValid);
    }

    [Fact]
    public void SearchAuditLogs_FromDateAfterToDate_ReturnsDateRangeInvalid_OtherwiseNoErrors()
    {
        var validator = new SearchAuditLogsRequestValidator();
        var backwards = new SearchAuditLogsRequest { FromDate = new DateOnly(2026, 10, 2), ToDate = new DateOnly(2026, 10, 1) };

        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(validator, backwards));
        Assert.Empty(ErrorCodesOf(validator, new SearchAuditLogsRequest { FromDate = new DateOnly(2026, 10, 1) }));
    }

    [Fact]
    public void GetScheduleRehearsals_NoneOrBothDates_AcceptsUpToMaxRange_RejectsTheRest()
    {
        var validator = new GetScheduleRehearsalsRequestValidator();
        var from = new DateOnly(2026, 10, 1);
        GetScheduleRehearsalsRequest Range(DateOnly? f, DateOnly? t) => new() { FromDate = f, ToDate = t };

        Assert.Empty(ErrorCodesOf(validator, Range(null, null)));
        Assert.Empty(ErrorCodesOf(validator, Range(from, from.AddDays(GetScheduleRehearsalsRequestValidator.MaxRangeDays - 1))));
        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(validator, Range(from, null)));
        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(validator, Range(null, from)));
        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(validator, Range(from, from.AddDays(-1))));
        Assert.Equal(
            [ErrorCodes.ReportDateRangeTooLarge],
            ErrorCodesOf(validator, Range(from, from.AddDays(GetScheduleRehearsalsRequestValidator.MaxRangeDays))));
    }

    [Fact]
    public void ExportReport_KnownTypeAndFormat_HasNoErrors()
    {
        var request = new ExportReportRequest { Type = ReportType.SongUsage, Format = ReportFormat.Pdf };

        Assert.Empty(ErrorCodesOf(new ExportReportRequestValidator(), request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData((ReportType)99)]
    public void ExportReport_MissingOrUnknownType_ReturnsTypeNotSupported(ReportType? type)
    {
        var codes = ErrorCodesOf(new ExportReportRequestValidator(), new ExportReportRequest { Type = type });

        Assert.Equal([ErrorCodes.ReportTypeNotSupported], codes);
    }

    [Fact]
    public void ExportReport_FromDateAfterToDate_ReturnsDateRangeInvalid()
    {
        var request = new ExportReportRequest
        {
            Type = ReportType.SongUsage, FromDate = new DateOnly(2026, 10, 2), ToDate = new DateOnly(2026, 10, 1),
        };

        Assert.Equal([ErrorCodes.ReportDateRangeInvalid], ErrorCodesOf(new ExportReportRequestValidator(), request));
    }

    // ---- Create rehearsal ----

    private static CreateRehearsalRequest NewRehearsal(params Guid[] songIds) => new()
    {
        ChoirId = Guid.NewGuid(),
        StartTime = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(2026, 12, 1, 14, 0, 0, DateTimeKind.Utc),
        Songs = songIds.Select(id => new UpdateRehearsalSongRequest { SongId = id }).ToList(),
    };

    [Fact]
    public void UpdateEventChoirs_LeadMustBeOneOfTheChoirs_AndEmptyListTakesNoLead()
    {
        var validator = new UpdateEventChoirsRequestValidator();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.Empty(ErrorCodesOf(validator, new UpdateEventChoirsRequest { ChoirIds = [a, b], LeadChoirId = b }));
        Assert.Empty(ErrorCodesOf(validator, new UpdateEventChoirsRequest { ChoirIds = [] }));
        Assert.Equal([ErrorCodes.ChoirLeadInvalid],
            ErrorCodesOf(validator, new UpdateEventChoirsRequest { ChoirIds = [a] }));
        Assert.Equal([ErrorCodes.ChoirLeadInvalid],
            ErrorCodesOf(validator, new UpdateEventChoirsRequest { ChoirIds = [a], LeadChoirId = Guid.NewGuid() }));
        Assert.Equal([ErrorCodes.ChoirLeadInvalid],
            ErrorCodesOf(validator, new UpdateEventChoirsRequest { ChoirIds = [], LeadChoirId = a }));
    }

    [Fact]
    public void CreateRehearsal_ValidWithOrWithoutSongs_HasNoErrors()
    {
        var validator = new CreateRehearsalRequestValidator();

        Assert.Empty(ErrorCodesOf(validator, NewRehearsal()));
        Assert.Empty(ErrorCodesOf(validator, NewRehearsal(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRehearsal_EndNotAfterStart_ReturnsTimeInvalid(int hoursAfterStart)
    {
        var request = NewRehearsal();
        request.EndTime = request.StartTime.AddHours(hoursAfterStart);

        Assert.Equal([ErrorCodes.RehearsalTimeInvalid], ErrorCodesOf(new CreateRehearsalRequestValidator(), request));
    }

    [Fact]
    public void CreateRehearsal_SameSongTwice_ReturnsSongDuplicate()
    {
        var id = Guid.NewGuid();

        var codes = ErrorCodesOf(new CreateRehearsalRequestValidator(), NewRehearsal(id, id));

        Assert.Equal([ErrorCodes.RehearsalSongDuplicate], codes);
    }

    [Fact]
    public void CreateRehearsal_MissingTimesEmptySongIdOrLongNote_IsInvalid()
    {
        var validator = new CreateRehearsalRequestValidator();
        var longNote = NewRehearsal();
        longNote.Note = new string('x', 501);

        Assert.False(validator.Validate(new CreateRehearsalRequest()).IsValid);
        Assert.False(validator.Validate(longNote).IsValid);
        Assert.False(validator.Validate(NewRehearsal(Guid.Empty)).IsValid);
    }

    // ---- Event participation ----

    [Theory]
    [InlineData(ParticipationStatus.Confirmed, true)]
    [InlineData(ParticipationStatus.Declined, true)]
    [InlineData(ParticipationStatus.Unsure, true)]
    [InlineData(ParticipationStatus.Invited, false)]
    [InlineData((ParticipationStatus)99, false)]
    public void RespondParticipation_OnlyARealAnswerIsValid(ParticipationStatus status, bool expected)
    {
        var result = new RespondParticipationRequestValidator().Validate(new RespondParticipationRequest { Status = status });

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void RespondParticipation_NoteOver300Chars_IsInvalid()
    {
        var request = new RespondParticipationRequest { Status = ParticipationStatus.Declined, Note = new string('x', 301) };

        Assert.False(new RespondParticipationRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void SendParticipationRequests_EventIdRequired_MemberIdsOptionalButNeverEmptyGuid()
    {
        var validator = new SendParticipationRequestsRequestValidator();
        var eventId = Guid.NewGuid();

        Assert.True(validator.Validate(new SendParticipationRequestsRequest { EventId = eventId }).IsValid);
        Assert.True(validator.Validate(new SendParticipationRequestsRequest { EventId = eventId, MemberIds = [Guid.NewGuid()] }).IsValid);
        Assert.False(validator.Validate(new SendParticipationRequestsRequest()).IsValid);
        Assert.False(validator.Validate(new SendParticipationRequestsRequest { EventId = eventId, MemberIds = [Guid.Empty] }).IsValid);
    }
    // ---- Search filters ----

    public static TheoryData<IValidator, object> SearchRequests() => new()
    {
        { new SearchUsersRequestValidator(), new SearchUsersRequest() },
        { new SearchSongsRequestValidator(), new SearchSongsRequest() },
        { new SearchPracticeSubmissionsRequestValidator(), new SearchPracticeSubmissionsRequest() },
        { new SearchMyPracticeSubmissionsRequestValidator(), new SearchMyPracticeSubmissionsRequest() },
        { new SearchMyPracticeAssignmentsRequestValidator(), new SearchMyPracticeAssignmentsRequest() },
        { new SearchMyParticipationHistoryRequestValidator(), new SearchMyParticipationHistoryRequest() },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest() },
        { new SearchMemberProfilesRequestValidator(), new SearchMemberProfilesRequest() },
        { new SearchMaterialLearningProgressRequestValidator(), new SearchMaterialLearningProgressRequest() },
        { new SearchLiturgicalEventsRequestValidator(), new SearchLiturgicalEventsRequest() },
        { new SearchDirectorNotesRequestValidator(), new SearchDirectorNotesRequest() },
    };

    public static TheoryData<IValidator, object> BadSearchRequests() => new()
    {
        { new SearchUsersRequestValidator(), new SearchUsersRequest { Keyword = new string('x', 101) } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { Keyword = new string('x', 101) } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { LiturgicalSeasonId = Guid.Empty } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { MassTypeId = Guid.Empty } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { CeremonyTypeId = Guid.Empty } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { SongThemeId = Guid.Empty } },
        { new SearchSongsRequestValidator(), new SearchSongsRequest { SkillId = Guid.Empty } },
        { new SearchPracticeSubmissionsRequestValidator(), new SearchPracticeSubmissionsRequest { Status = (SubmissionStatus)99 } },
        { new SearchMyPracticeSubmissionsRequestValidator(), new SearchMyPracticeSubmissionsRequest { AssignmentId = Guid.Empty } },
        { new SearchMyPracticeAssignmentsRequestValidator(), new SearchMyPracticeAssignmentsRequest { Status = (SubmissionStatus)99 } },
        { new SearchMyParticipationHistoryRequestValidator(), new SearchMyParticipationHistoryRequest { LiturgicalSeasonId = Guid.Empty } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { Keyword = new string('x', 101) } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { SongId = Guid.Empty } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { LiturgicalSeasonId = Guid.Empty } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { SkillId = Guid.Empty } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { MaterialType = (MaterialType)99 } },
        { new SearchMusicMaterialsRequestValidator(), new SearchMusicMaterialsRequest { LearningStatus = (LearningStatus)99 } },
        { new SearchMemberProfilesRequestValidator(), new SearchMemberProfilesRequest { Keyword = new string('x', 101) } },
        { new SearchMemberProfilesRequestValidator(), new SearchMemberProfilesRequest { Status = (MemberStatus)99 } },
        { new SearchMemberProfilesRequestValidator(), new SearchMemberProfilesRequest { SkillId = Guid.Empty } },
        { new SearchMaterialLearningProgressRequestValidator(), new SearchMaterialLearningProgressRequest { Status = (LearningStatus)99 } },
        { new SearchLiturgicalEventsRequestValidator(), new SearchLiturgicalEventsRequest { Status = (EventStatus)99 } },
        { new SearchDirectorNotesRequestValidator(), new SearchDirectorNotesRequest { EventId = Guid.Empty } },
    };

    private static bool IsValid(IValidator validator, object request) =>
        validator.Validate(new ValidationContext<object>(request)).IsValid;

    [Theory]
    [MemberData(nameof(SearchRequests))]
    public void Search_NoFilters_IsValid(IValidator validator, object request) => Assert.True(IsValid(validator, request));

    [Theory]
    [MemberData(nameof(BadSearchRequests))]
    public void Search_LongKeywordEmptyGuidOrUnknownEnum_IsInvalid(IValidator validator, object request) =>
        Assert.False(IsValid(validator, request));

    [Fact]
    public void Search_EveryFilterSet_IsValid()
    {
        var songs = new SearchSongsRequest { Keyword = new string('x', 100), SkillId = Guid.NewGuid() };
        var materials = new SearchMusicMaterialsRequest { MaterialType = MaterialType.Lyrics, LearningStatus = LearningStatus.Learned };

        Assert.True(IsValid(new SearchSongsRequestValidator(), songs));
        Assert.True(IsValid(new SearchMusicMaterialsRequestValidator(), materials));
    }

    [Fact]
    public void SearchLiturgicalEvents_FromDateAfterToDate_ReturnsDateRangeInvalid_OtherwiseNoErrors()
    {
        var validator = new SearchLiturgicalEventsRequestValidator();
        var day = new DateOnly(2026, 10, 1);

        Assert.Equal(
            [ErrorCodes.ReportDateRangeInvalid],
            ErrorCodesOf(validator, new SearchLiturgicalEventsRequest { FromDate = day.AddDays(1), ToDate = day }));
        Assert.Empty(ErrorCodesOf(validator, new SearchLiturgicalEventsRequest { FromDate = day, ToDate = day, Status = EventStatus.Published }));
    }

    [Fact]
    public void SearchMyParticipationHistory_FromDateAfterToDate_ReturnsDateRangeInvalid_OtherwiseNoErrors()
    {
        var validator = new SearchMyParticipationHistoryRequestValidator();
        var day = new DateOnly(2026, 10, 1);

        Assert.Equal(
            [ErrorCodes.ReportDateRangeInvalid],
            ErrorCodesOf(validator, new SearchMyParticipationHistoryRequest { FromDate = day.AddDays(1), ToDate = day }));
        Assert.Empty(ErrorCodesOf(validator, new SearchMyParticipationHistoryRequest { FromDate = day }));
    }

    // ---- Generate liturgical events ----

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 61, true)]
    [InlineData(0, 62, false)]
    [InlineData(-1, 3, false)]
    [InlineData(3, 2, false)]
    public void GenerateLiturgicalEvents_RangeFromTodayUpTo62Days(int fromOffset, int toOffset, bool isValid)
    {
        var codes = ErrorCodesOf(new GenerateLiturgicalEventsRequestValidator(), new GenerateLiturgicalEventsRequest
        {
            FromDate = VietnamTime.Today.AddDays(fromOffset), ToDate = VietnamTime.Today.AddDays(toOffset),
        });

        Assert.Equal(isValid ? [] : [ErrorCodes.LiturgicalEventGenerationRangeInvalid], codes);
    }
}

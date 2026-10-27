using Harmonia.Domain.Common;

namespace Harmonia.API.Middlewares;

/// <summary>
/// The single error code -> HTTP status table, transcribed from the HTTP column of
/// doc/error-codes.md. Both the exception middleware and controllers returning
/// <c>Result.Failure</c> read it, so a code's status is defined in exactly one place.
/// The fallback only catches a code missing from this table: 409 for a domain exception,
/// 400 for a service failure. Field-level FluentValidation codes are absent by design:
/// they ride inside a 400 response's "errors" map and never reach this table.
/// </summary>
public static class ErrorStatusMap
{
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        // 0. Shared.
        [ErrorCodes.ValidationFailed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.Conflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.InternalError] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.LookupNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.LookupNameDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.LookupInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.LookupInactive] = StatusCodes.Status409Conflict,

        // 1. Auth.
        [ErrorCodes.AuthInvalidCredentials] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthAccountInactive] = StatusCodes.Status403Forbidden,
        [ErrorCodes.AuthPasswordChangeRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.AuthTokenInvalid] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthTokenExpired] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthRefreshTokenNotFound] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthRefreshTokenRevoked] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthRefreshTokenExpired] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthCurrentPasswordInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthPasswordTooWeak] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthResetTokenInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthResetTokenExpired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthResetTokenUsed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.AuthGoogleTokenInvalid] = StatusCodes.Status401Unauthorized,

        // 2. User & Role.
        [ErrorCodes.UserNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.UserEmailAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserAlreadyInactive] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserAlreadyActive] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserCannotModifySelf] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserLastAdmin] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoleNotFound] = StatusCodes.Status404NotFound,

        // 3. MemberProfile.
        [ErrorCodes.MemberNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MemberProfileAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberNotActive] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberJoinedDateInFuture] = StatusCodes.Status400BadRequest,

        // 4. Skill & MemberSkill.
        [ErrorCodes.SkillNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SkillNameDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.SkillInactive] = StatusCodes.Status409Conflict,
        [ErrorCodes.SkillInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.SkillCategoryNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MemberSkillNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MemberSkillAlreadyDeclared] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberSkillAlreadyReviewed] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberSkillRejectReasonRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberSkillNotApproved] = StatusCodes.Status409Conflict,

        // 5. Liturgical calendar.
        [ErrorCodes.CalendarDayNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.LiturgicalNameTranslationNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.LiturgicalNameTranslationDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.LiturgicalEventSlotTaken] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventAlreadyPublished] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventNotPublished] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventCancelled] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventAlreadyPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalEventTypeRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason] = StatusCodes.Status400BadRequest,
        [ErrorCodes.LiturgicalEventSeasonRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.LiturgicalEventGenerationRangeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.LiturgicalSeasonDateOverlap] = StatusCodes.Status409Conflict,
        [ErrorCodes.LiturgicalSeasonDateInvalid] = StatusCodes.Status400BadRequest,

        // 6. Song library.
        [ErrorCodes.SongNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SongInactive] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongTitleDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongClassificationTargetInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SongThemeParentInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SongThemeHasActiveChildren] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongSkillRequirementCategoryInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MaterialNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MaterialFileRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MaterialFileTypeNotAllowed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MaterialFileTooLarge] = StatusCodes.Status413PayloadTooLarge,
        [ErrorCodes.MaterialLearningStatusInvalid] = StatusCodes.Status400BadRequest,

        // 7. Song list & review.
        [ErrorCodes.SongListNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SongListNotEditable] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListNotLatestVersion] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListEmpty] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SongListAlreadySubmitted] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListNotSubmitted] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListAlreadyApproved] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListEventHasApprovedVersion] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongListItemNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SongListSlotDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.SlotNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SongListCannotBeRevised] = StatusCodes.Status409Conflict,
        [ErrorCodes.ReviewNotesRequired] = StatusCodes.Status400BadRequest,

        // 8. Event participation.
        [ErrorCodes.EventParticipationNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.EventParticipationAlreadyRequested] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventParticipationAlreadyResponded] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventParticipationClosed] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventParticipationNotInvited] = StatusCodes.Status409Conflict,

        // 9. Service roster.
        [ErrorCodes.RosterNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RosterAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterAlreadyFinalized] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterNotFinalized] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterSongListNotApproved] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterNoPersonnelRequirement] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterInsufficientMembers] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterAssignmentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RosterAssignmentMemberSkillNotApproved] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterAssignmentMemberNotConfirmed] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterAssignmentDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterMemberTimeConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.PersonnelRequirementNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PersonnelRequirementInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.PersonnelRequiredCountInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PersonnelRequirementDuplicate] = StatusCodes.Status409Conflict,

        // 10. Rehearsal & attendance.
        [ErrorCodes.RehearsalNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RehearsalTimeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RehearsalTimeConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalAlreadyPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalHasAttendance] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalAttendanceNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RehearsalAttendanceAlreadyRecorded] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalNotStarted] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalAttendanceMemberDuplicate] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RehearsalSongDuplicate] = StatusCodes.Status400BadRequest,

        // 11. Practice assignment & submission.
        [ErrorCodes.PracticeAssignmentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PracticeDueDateInPast] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeTargetRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeNotAssignedToMember] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PracticeSubmissionPastDue] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionAlreadyReviewed] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionAlreadyPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionSuperseded] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionNotReviewed] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeAudioRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeAudioTypeNotAllowed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeAudioTooLarge] = StatusCodes.Status413PayloadTooLarge,

        // 12. Notification.
        [ErrorCodes.NotificationNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DirectorNoteTargetRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.DirectorNoteNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DirectorNoteRecipientInvalid] = StatusCodes.Status400BadRequest,

        // 13. System settings & reports.
        [ErrorCodes.SettingNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SettingValueTypeMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SettingValueOutOfRange] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportTypeNotSupported] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportDateRangeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportDateRangeTooLarge] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportNoData] = StatusCodes.Status404NotFound,

        // 14. External services.
        [ErrorCodes.ExternalEmailFailed] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ExternalStorageFailed] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ExternalAiFailed] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ExternalCalendarFailed] = StatusCodes.Status502BadGateway,

        // 15. Choir.
        [ErrorCodes.ChoirNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.ChoirNameAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.ChoirMemberInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ChoirInactive] = StatusCodes.Status409Conflict,
        [ErrorCodes.ChoirMembershipNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.ChoirDirectorInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ChoirNotServingEvent] = StatusCodes.Status409Conflict,
        [ErrorCodes.ChoirLeadInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ChoirNotLead] = StatusCodes.Status403Forbidden,

        // 16. Mass schedule.
        [ErrorCodes.MassScheduleNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MassScheduleSlotTaken] = StatusCodes.Status409Conflict,
    };

    public static int StatusFor(string code, int fallback) => StatusByCode.GetValueOrDefault(code, fallback);
}

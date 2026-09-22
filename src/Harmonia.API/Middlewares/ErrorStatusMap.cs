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

        // 2. User & Role.
        [ErrorCodes.UserNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.UserEmailAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserAlreadyInactive] = StatusCodes.Status409Conflict,
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
        [ErrorCodes.SkillCategoryNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MemberSkillNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.MemberSkillAlreadyDeclared] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberSkillAlreadyReviewed] = StatusCodes.Status409Conflict,
        [ErrorCodes.MemberSkillRejectReasonRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.MemberSkillNotApproved] = StatusCodes.Status409Conflict,

        // 5. Liturgical calendar.
        [ErrorCodes.WeekNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.WeekAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.WeekStartNotMonday] = StatusCodes.Status400BadRequest,
        [ErrorCodes.WeekAlreadyPublished] = StatusCodes.Status409Conflict,
        [ErrorCodes.WeekNotPublished] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.EventSlotTaken] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventDateOutsideWeek] = StatusCodes.Status400BadRequest,
        [ErrorCodes.EventCancelled] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventAlreadyPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.EventTypeRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SeasonDateOverlap] = StatusCodes.Status409Conflict,
        [ErrorCodes.SeasonDateInvalid] = StatusCodes.Status400BadRequest,

        // 6. Song library.
        [ErrorCodes.SongNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SongInactive] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongTitleDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongClassificationDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.SongClassificationTargetInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.SongSkillRequirementDuplicate] = StatusCodes.Status409Conflict,
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
        [ErrorCodes.ParticipationNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.ParticipationAlreadyRequested] = StatusCodes.Status409Conflict,
        [ErrorCodes.ParticipationAlreadyResponded] = StatusCodes.Status409Conflict,
        [ErrorCodes.ParticipationEventPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.ParticipationNotInvited] = StatusCodes.Status409Conflict,

        // 9. Service roster.
        [ErrorCodes.RosterNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RosterAlreadyExists] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterAlreadyFinalized] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterSongListNotApproved] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterNoPersonnelRequirement] = StatusCodes.Status409Conflict,
        [ErrorCodes.RosterInsufficientMembers] = StatusCodes.Status409Conflict,
        [ErrorCodes.AssignmentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.AssignmentMemberSkillNotApproved] = StatusCodes.Status409Conflict,
        [ErrorCodes.AssignmentMemberNotConfirmed] = StatusCodes.Status409Conflict,
        [ErrorCodes.AssignmentDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.PersonnelRequirementNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PersonnelRequiredCountInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PersonnelRequirementDuplicate] = StatusCodes.Status409Conflict,

        // 10. Rehearsal & attendance.
        [ErrorCodes.RehearsalNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RehearsalTimeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.RehearsalTimeConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.RehearsalAlreadyPassed] = StatusCodes.Status409Conflict,
        [ErrorCodes.AttendanceNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.AttendanceAlreadyRecorded] = StatusCodes.Status409Conflict,

        // 11. Practice assignment & submission.
        [ErrorCodes.PracticeAssignmentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PracticeDueDateInPast] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeTargetRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeNotAssignedToMember] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.PracticeSubmissionPastDue] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeSubmissionAlreadyReviewed] = StatusCodes.Status409Conflict,
        [ErrorCodes.PracticeAudioRequired] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeAudioTypeNotAllowed] = StatusCodes.Status400BadRequest,
        [ErrorCodes.PracticeAudioTooLarge] = StatusCodes.Status413PayloadTooLarge,

        // 12. Notification.
        [ErrorCodes.NotificationNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DirectorNoteTargetRequired] = StatusCodes.Status400BadRequest,

        // 13. System settings & reports.
        [ErrorCodes.SettingNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.SettingValueTypeMismatch] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportTypeNotSupported] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportDateRangeInvalid] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportDateRangeTooLarge] = StatusCodes.Status400BadRequest,
        [ErrorCodes.ReportNoData] = StatusCodes.Status404NotFound,

        // 14. External services.
        [ErrorCodes.ExternalEmailFailed] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ExternalStorageFailed] = StatusCodes.Status502BadGateway,
        [ErrorCodes.ExternalAiFailed] = StatusCodes.Status502BadGateway,
    };

    public static int StatusFor(string code, int fallback) => StatusByCode.GetValueOrDefault(code, fallback);
}

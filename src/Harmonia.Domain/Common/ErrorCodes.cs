namespace Harmonia.Domain.Common;

/// <summary>
/// Business error codes returned to clients. The full catalogue and the Vietnamese wording
/// live in doc/error-codes.md, which is the contract with the frontend.
/// Every code in that catalogue is declared here up front, and every code that reaches a
/// client through a status other than its layer's default is listed in <c>ErrorStatusMap</c>.
/// Sections below follow the sections of doc/error-codes.md.
/// </summary>
public static class ErrorCodes
{
    // 0. Shared by every endpoint.
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string Unauthorized = "UNAUTHORIZED";

    public const string Forbidden = "FORBIDDEN";

    public const string NotFound = "NOT_FOUND";

    public const string Conflict = "CONFLICT";

    public const string InternalError = "INTERNAL_ERROR";

    // Shared by the nine Admin-configured lookup tables.
    public const string LookupNotFound = "LOOKUP_NOT_FOUND";

    public const string LookupNameDuplicate = "LOOKUP_NAME_DUPLICATE";

    public const string LookupInUse = "LOOKUP_IN_USE";

    public const string LookupInactive = "LOOKUP_INACTIVE";

    // 1. Auth.
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";

    public const string AuthAccountInactive = "AUTH_ACCOUNT_INACTIVE";

    public const string AuthTokenInvalid = "AUTH_TOKEN_INVALID";

    public const string AuthTokenExpired = "AUTH_TOKEN_EXPIRED";

    public const string AuthRefreshTokenNotFound = "AUTH_REFRESH_TOKEN_NOT_FOUND";

    public const string AuthRefreshTokenRevoked = "AUTH_REFRESH_TOKEN_REVOKED";

    public const string AuthRefreshTokenExpired = "AUTH_REFRESH_TOKEN_EXPIRED";

    public const string AuthCurrentPasswordInvalid = "AUTH_CURRENT_PASSWORD_INVALID";

    public const string AuthPasswordTooWeak = "AUTH_PASSWORD_TOO_WEAK";

    public const string AuthResetTokenInvalid = "AUTH_RESET_TOKEN_INVALID";

    public const string AuthResetTokenExpired = "AUTH_RESET_TOKEN_EXPIRED";

    public const string AuthResetTokenUsed = "AUTH_RESET_TOKEN_USED";

    // Field-level codes raised by FluentValidation; they always travel inside a 400 response's
    // "errors" map, so they never appear in ErrorStatusMap.
    public const string AuthEmailRequired = "AUTH_EMAIL_REQUIRED";

    public const string AuthEmailInvalidFormat = "AUTH_EMAIL_INVALID_FORMAT";

    public const string AuthPasswordRequired = "AUTH_PASSWORD_REQUIRED";

    public const string AuthRefreshTokenRequired = "AUTH_REFRESH_TOKEN_REQUIRED";

    // 2. User & Role (Admin).
    public const string UserNotFound = "USER_NOT_FOUND";

    public const string UserEmailAlreadyExists = "USER_EMAIL_ALREADY_EXISTS";

    public const string UserAlreadyInactive = "USER_ALREADY_INACTIVE";

    public const string UserCannotModifySelf = "USER_CANNOT_MODIFY_SELF";

    public const string UserLastAdmin = "USER_LAST_ADMIN";

    public const string RoleNotFound = "ROLE_NOT_FOUND";

    // 3. MemberProfile.
    public const string MemberNotFound = "MEMBER_NOT_FOUND";

    public const string MemberProfileAlreadyExists = "MEMBER_PROFILE_ALREADY_EXISTS";

    public const string MemberNotActive = "MEMBER_NOT_ACTIVE";

    public const string MemberJoinedDateInFuture = "MEMBER_JOINED_DATE_IN_FUTURE";

    // 4. Skill & MemberSkill.
    public const string SkillNotFound = "SKILL_NOT_FOUND";

    public const string SkillNameDuplicate = "SKILL_NAME_DUPLICATE";

    public const string SkillInactive = "SKILL_INACTIVE";

    public const string SkillCategoryNotFound = "SKILL_CATEGORY_NOT_FOUND";

    public const string MemberSkillNotFound = "MEMBER_SKILL_NOT_FOUND";

    public const string MemberSkillAlreadyDeclared = "MEMBER_SKILL_ALREADY_DECLARED";

    public const string MemberSkillAlreadyReviewed = "MEMBER_SKILL_ALREADY_REVIEWED";

    public const string MemberSkillRejectReasonRequired = "MEMBER_SKILL_REJECT_REASON_REQUIRED";

    public const string MemberSkillNotApproved = "MEMBER_SKILL_NOT_APPROVED";

    // 5. Liturgical calendar.
    public const string WeekNotFound = "WEEK_NOT_FOUND";

    public const string WeekAlreadyExists = "WEEK_ALREADY_EXISTS";

    public const string WeekStartNotMonday = "WEEK_START_NOT_MONDAY";

    public const string WeekAlreadyPublished = "WEEK_ALREADY_PUBLISHED";

    public const string WeekNotPublished = "WEEK_NOT_PUBLISHED";

    public const string EventNotFound = "EVENT_NOT_FOUND";

    public const string EventSlotTaken = "EVENT_SLOT_TAKEN";

    public const string EventDateOutsideWeek = "EVENT_DATE_OUTSIDE_WEEK";

    public const string EventCancelled = "EVENT_CANCELLED";

    public const string EventAlreadyPassed = "EVENT_ALREADY_PASSED";

    public const string EventTypeRequired = "EVENT_TYPE_REQUIRED";

    public const string SeasonDateOverlap = "SEASON_DATE_OVERLAP";

    public const string SeasonDateInvalid = "SEASON_DATE_INVALID";

    // 6. Song library.
    public const string SongNotFound = "SONG_NOT_FOUND";

    public const string SongInactive = "SONG_INACTIVE";

    public const string SongTitleDuplicate = "SONG_TITLE_DUPLICATE";

    public const string SongClassificationDuplicate = "SONG_CLASSIFICATION_DUPLICATE";

    public const string SongClassificationTargetInvalid = "SONG_CLASSIFICATION_TARGET_INVALID";

    public const string SongSkillRequirementDuplicate = "SONG_SKILL_REQUIREMENT_DUPLICATE";

    public const string MaterialNotFound = "MATERIAL_NOT_FOUND";

    public const string MaterialFileRequired = "MATERIAL_FILE_REQUIRED";

    public const string MaterialFileTypeNotAllowed = "MATERIAL_FILE_TYPE_NOT_ALLOWED";

    public const string MaterialFileTooLarge = "MATERIAL_FILE_TOO_LARGE";

    public const string MaterialLearningStatusInvalid = "MATERIAL_LEARNING_STATUS_INVALID";

    // 7. Song list & review.
    public const string SongListNotFound = "SONG_LIST_NOT_FOUND";

    public const string SongListNotEditable = "SONG_LIST_NOT_EDITABLE";

    public const string SongListNotLatestVersion = "SONG_LIST_NOT_LATEST_VERSION";

    public const string SongListEmpty = "SONG_LIST_EMPTY";

    public const string SongListAlreadySubmitted = "SONG_LIST_ALREADY_SUBMITTED";

    public const string SongListNotSubmitted = "SONG_LIST_NOT_SUBMITTED";

    public const string SongListAlreadyApproved = "SONG_LIST_ALREADY_APPROVED";

    public const string SongListEventHasApprovedVersion = "SONG_LIST_EVENT_HAS_APPROVED_VERSION";

    public const string SongListItemNotFound = "SONG_LIST_ITEM_NOT_FOUND";

    public const string SongListSlotDuplicate = "SONG_LIST_SLOT_DUPLICATE";

    public const string SlotNotFound = "SLOT_NOT_FOUND";

    public const string SongListCannotBeRevised = "SONG_LIST_CANNOT_BE_REVISED";

    public const string ReviewNotesRequired = "REVIEW_NOTES_REQUIRED";

    // 8. Event participation.
    public const string ParticipationNotFound = "PARTICIPATION_NOT_FOUND";

    public const string ParticipationAlreadyRequested = "PARTICIPATION_ALREADY_REQUESTED";

    public const string ParticipationAlreadyResponded = "PARTICIPATION_ALREADY_RESPONDED";

    public const string ParticipationEventPassed = "PARTICIPATION_EVENT_PASSED";

    public const string ParticipationNotInvited = "PARTICIPATION_NOT_INVITED";

    // 9. Service roster.
    public const string RosterNotFound = "ROSTER_NOT_FOUND";

    public const string RosterAlreadyExists = "ROSTER_ALREADY_EXISTS";

    public const string RosterAlreadyFinalized = "ROSTER_ALREADY_FINALIZED";

    public const string RosterSongListNotApproved = "ROSTER_SONG_LIST_NOT_APPROVED";

    public const string RosterNoPersonnelRequirement = "ROSTER_NO_PERSONNEL_REQUIREMENT";

    public const string RosterInsufficientMembers = "ROSTER_INSUFFICIENT_MEMBERS";

    public const string AssignmentNotFound = "ASSIGNMENT_NOT_FOUND";

    public const string AssignmentMemberSkillNotApproved = "ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED";

    public const string AssignmentMemberNotConfirmed = "ASSIGNMENT_MEMBER_NOT_CONFIRMED";

    public const string AssignmentDuplicate = "ASSIGNMENT_DUPLICATE";

    public const string PersonnelRequirementNotFound = "PERSONNEL_REQUIREMENT_NOT_FOUND";

    public const string PersonnelRequiredCountInvalid = "PERSONNEL_REQUIRED_COUNT_INVALID";

    public const string PersonnelRequirementDuplicate = "PERSONNEL_REQUIREMENT_DUPLICATE";

    // 10. Rehearsal & attendance.
    public const string RehearsalNotFound = "REHEARSAL_NOT_FOUND";

    public const string RehearsalTimeInvalid = "REHEARSAL_TIME_INVALID";

    public const string RehearsalTimeConflict = "REHEARSAL_TIME_CONFLICT";

    public const string RehearsalAlreadyPassed = "REHEARSAL_ALREADY_PASSED";

    public const string AttendanceNotFound = "ATTENDANCE_NOT_FOUND";

    public const string AttendanceAlreadyRecorded = "ATTENDANCE_ALREADY_RECORDED";

    // 11. Practice assignment & submission.
    public const string PracticeAssignmentNotFound = "PRACTICE_ASSIGNMENT_NOT_FOUND";

    public const string PracticeDueDateInPast = "PRACTICE_DUE_DATE_IN_PAST";

    public const string PracticeTargetRequired = "PRACTICE_TARGET_REQUIRED";

    public const string PracticeNotAssignedToMember = "PRACTICE_NOT_ASSIGNED_TO_MEMBER";

    public const string PracticeSubmissionNotFound = "PRACTICE_SUBMISSION_NOT_FOUND";

    public const string PracticeSubmissionPastDue = "PRACTICE_SUBMISSION_PAST_DUE";

    public const string PracticeSubmissionAlreadyReviewed = "PRACTICE_SUBMISSION_ALREADY_REVIEWED";

    public const string PracticeAudioRequired = "PRACTICE_AUDIO_REQUIRED";

    public const string PracticeAudioTypeNotAllowed = "PRACTICE_AUDIO_TYPE_NOT_ALLOWED";

    public const string PracticeAudioTooLarge = "PRACTICE_AUDIO_TOO_LARGE";

    // 12. Notification.
    // A notification belonging to someone else is reported as NOTIFICATION_NOT_FOUND, so there is
    // deliberately no "not for you" code: a 403 would confirm the record exists.
    public const string NotificationNotFound = "NOTIFICATION_NOT_FOUND";

    public const string DirectorNoteTargetRequired = "DIRECTOR_NOTE_TARGET_REQUIRED";

    // 13. System settings & reports.
    public const string SettingNotFound = "SETTING_NOT_FOUND";

    public const string SettingValueTypeMismatch = "SETTING_VALUE_TYPE_MISMATCH";

    public const string ReportTypeNotSupported = "REPORT_TYPE_NOT_SUPPORTED";

    public const string ReportDateRangeInvalid = "REPORT_DATE_RANGE_INVALID";

    public const string ReportDateRangeTooLarge = "REPORT_DATE_RANGE_TOO_LARGE";

    public const string ReportNoData = "REPORT_NO_DATA";

    // 14. External services.
    public const string ExternalEmailFailed = "EXTERNAL_EMAIL_FAILED";

    public const string ExternalStorageFailed = "EXTERNAL_STORAGE_FAILED";

    public const string ExternalAiFailed = "EXTERNAL_AI_FAILED";
}

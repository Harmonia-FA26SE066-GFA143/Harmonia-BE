namespace Harmonia.Domain.Common;

/// <summary>
/// Business error codes returned to clients. The full catalogue and the Vietnamese wording
/// live in doc/error-codes.md, which is the contract with the frontend.
/// Shared codes plus every code thrown by a domain exception; codes returned only by
/// services are added when the feature that returns them is built.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Shared by every endpoint.</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    public const string Unauthorized = "UNAUTHORIZED";

    public const string Forbidden = "FORBIDDEN";

    public const string NotFound = "NOT_FOUND";

    public const string Conflict = "CONFLICT";

    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>Shared by the nine Admin-configured lookup tables.</summary>
    public const string LookupNotFound = "LOOKUP_NOT_FOUND";

    public const string LookupNameDuplicate = "LOOKUP_NAME_DUPLICATE";

    public const string LookupInUse = "LOOKUP_IN_USE";

    public const string LookupInactive = "LOOKUP_INACTIVE";

    public const string UserAlreadyInactive = "USER_ALREADY_INACTIVE";

    public const string AuthRefreshTokenRevoked = "AUTH_REFRESH_TOKEN_REVOKED";

    public const string AuthRefreshTokenExpired = "AUTH_REFRESH_TOKEN_EXPIRED";

    public const string MemberNotActive = "MEMBER_NOT_ACTIVE";

    public const string MemberJoinedDateInFuture = "MEMBER_JOINED_DATE_IN_FUTURE";

    public const string MemberSkillAlreadyReviewed = "MEMBER_SKILL_ALREADY_REVIEWED";

    public const string MemberSkillRejectReasonRequired = "MEMBER_SKILL_REJECT_REASON_REQUIRED";

    public const string SkillInactive = "SKILL_INACTIVE";

    public const string WeekStartNotMonday = "WEEK_START_NOT_MONDAY";

    public const string WeekAlreadyPublished = "WEEK_ALREADY_PUBLISHED";

    public const string WeekNotPublished = "WEEK_NOT_PUBLISHED";

    public const string EventDateOutsideWeek = "EVENT_DATE_OUTSIDE_WEEK";

    public const string EventTypeRequired = "EVENT_TYPE_REQUIRED";

    public const string EventCancelled = "EVENT_CANCELLED";

    public const string EventAlreadyPassed = "EVENT_ALREADY_PASSED";

    public const string SeasonDateInvalid = "SEASON_DATE_INVALID";

    public const string SongInactive = "SONG_INACTIVE";

    public const string SongListNotEditable = "SONG_LIST_NOT_EDITABLE";

    public const string SongListSlotDuplicate = "SONG_LIST_SLOT_DUPLICATE";

    public const string SongListEmpty = "SONG_LIST_EMPTY";

    public const string SongListAlreadySubmitted = "SONG_LIST_ALREADY_SUBMITTED";

    public const string SongListNotSubmitted = "SONG_LIST_NOT_SUBMITTED";

    public const string SongListAlreadyApproved = "SONG_LIST_ALREADY_APPROVED";

    public const string ReviewNotesRequired = "REVIEW_NOTES_REQUIRED";

    public const string SongListCannotBeRevised = "SONG_LIST_CANNOT_BE_REVISED";

    public const string ParticipationAlreadyResponded = "PARTICIPATION_ALREADY_RESPONDED";

    public const string ParticipationEventPassed = "PARTICIPATION_EVENT_PASSED";

    public const string RosterAlreadyFinalized = "ROSTER_ALREADY_FINALIZED";

    public const string AssignmentDuplicate = "ASSIGNMENT_DUPLICATE";

    public const string PersonnelRequiredCountInvalid = "PERSONNEL_REQUIRED_COUNT_INVALID";

    public const string RehearsalTimeInvalid = "REHEARSAL_TIME_INVALID";

    public const string RehearsalAlreadyPassed = "REHEARSAL_ALREADY_PASSED";

    public const string AttendanceAlreadyRecorded = "ATTENDANCE_ALREADY_RECORDED";

    public const string PracticeDueDateInPast = "PRACTICE_DUE_DATE_IN_PAST";

    public const string PracticeTargetRequired = "PRACTICE_TARGET_REQUIRED";

    public const string PracticeSubmissionPastDue = "PRACTICE_SUBMISSION_PAST_DUE";

    public const string PracticeSubmissionAlreadyReviewed = "PRACTICE_SUBMISSION_ALREADY_REVIEWED";

    public const string DirectorNoteTargetRequired = "DIRECTOR_NOTE_TARGET_REQUIRED";

    public const string SettingValueTypeMismatch = "SETTING_VALUE_TYPE_MISMATCH";
}

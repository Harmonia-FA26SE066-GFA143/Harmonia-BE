# Harmonia – Domain Exception Catalogue

Suy ra từ `harmonia-domain-entity-list.md` (42 entity, 21 enum) và `error-codes.md`.

**Tiêu chí:** domain exception chỉ gồm những luật mà entity **tự kiểm tra được bằng dữ
liệu của chính nó** (hoặc của aggregate nó đang giữ), không cần truy vấn DB. Lỗi cần DB
(trùng, không tìm thấy, chồng lấn, liên quan aggregate khác) thuộc Application — trả qua
`Result<T>` hoặc `NotFoundException`.

Thư mục đích: `src/Harmonia.Domain/Exceptions/`. Đặt tên theo `.claude/rules/02-naming.md`:
`<Nội dung>Exception`. Message exception bằng tiếng Anh.

**Danh sách này là tham chiếu, không phải checklist.** Chỉ tạo class khi làm tới chức năng
tương ứng.

---

## Lớp gốc

```csharp
namespace Harmonia.Domain.Exceptions;

public abstract class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
```

Phương án A (đã chốt 2026-09-21): entity ném, Service **không** bắt,
`GlobalExceptionHandlingMiddleware` bắt `DomainException` và map `Code` → HTTP theo
`error-codes.md` (mặc định 409). Mã lấy từ `Domain/Common/ErrorCodes.cs`.

---

## 1. Identity & thành viên

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `UserAlreadyInactiveException` | `User.Deactivate()` khi đã vô hiệu | `USER_ALREADY_INACTIVE` | 409 |
| `RefreshTokenRevokedException` | Dùng token đã có `RevokedAt` | `AUTH_REFRESH_TOKEN_REVOKED` | 401 |
| `RefreshTokenExpiredException` | `now > ExpiresAt` | `AUTH_REFRESH_TOKEN_EXPIRED` | 401 |
| `MemberNotActiveException` | Thao tác trên ca viên `Status ≠ Active` | `MEMBER_NOT_ACTIVE` | 409 |
| `MemberJoinedDateInFutureException` | `JoinedDate > today` | `MEMBER_JOINED_DATE_IN_FUTURE` | 400 |

## 2. Kỹ năng & danh mục

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `MemberSkillAlreadyReviewedException` | Approve/Reject khi `Status ≠ Pending` | `MEMBER_SKILL_ALREADY_REVIEWED` | 409 |
| `MemberSkillRejectReasonRequiredException` | Reject không kèm lý do | `MEMBER_SKILL_REJECT_REASON_REQUIRED` | 400 |
| `LookupInactiveException` | Gán mục danh mục `IsActive = false` (dùng chung 9 bảng lookup) | `LOOKUP_INACTIVE` | 409 |
| `SkillInactiveException` | Khai báo / phân công theo `Skill` có `IsActive = false` | `SKILL_INACTIVE` | 409 |

## 3. Lịch phụng vụ

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `WeekStartNotMondayException` | `WeekStartDate` không phải thứ Hai | `WEEK_START_NOT_MONDAY` | 400 |
| `WeekAlreadyPublishedException` | Sửa tuần / thêm-xoá sự kiện sau khi công bố | `WEEK_ALREADY_PUBLISHED` | 409 |
| `WeekNotPublishedException` | Thao tác đòi hỏi tuần đã công bố | `WEEK_NOT_PUBLISHED` | 409 |
| `EventDateOutsideWeekException` | `EventDate` ngoài khoảng Start–End của tuần | `EVENT_DATE_OUTSIDE_WEEK` | 400 |
| `EventTypeRequiredException` | Không có cả `MassTypeId` lẫn `CeremonyTypeId` | `EVENT_TYPE_REQUIRED` | 400 |
| `EventCancelledException` | Thao tác trên sự kiện đã huỷ | `EVENT_CANCELLED` | 409 |
| `EventAlreadyPassedException` | Sửa sự kiện đã diễn ra | `EVENT_ALREADY_PASSED` | 409 |
| `SeasonDateInvalidException` | `EndDate <= StartDate` | `SEASON_DATE_INVALID` | 400 |

## 4. Thư viện & danh sách bài hát

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `SongInactiveException` | Thêm bài hát đã vô hiệu vào danh sách | `SONG_INACTIVE` | 409 |
| `SongListNotEditableException` | Thêm/sửa/xoá item khi `Status ≠ Draft` | `SONG_LIST_NOT_EDITABLE` | 409 |
| `SongListSlotDuplicateException` | Hai item trùng `SlotId` | `SONG_LIST_SLOT_DUPLICATE` | 409 |
| `SongListEmptyException` | Gửi duyệt khi chưa có item | `SONG_LIST_EMPTY` | 400 |
| `SongListAlreadySubmittedException` | Gửi duyệt lần hai | `SONG_LIST_ALREADY_SUBMITTED` | 409 |
| `SongListNotSubmittedException` | Linh mục duyệt khi `Status ≠ Submitted` (gồm cả duyệt lại bản đã quyết định) | `SONG_LIST_NOT_SUBMITTED` | 409 |
| `SongListAlreadyApprovedException` | Thao tác trên danh sách đã duyệt | `SONG_LIST_ALREADY_APPROVED` | 409 |
| `ReviewNotesRequiredException` | Reject/RequestRevision không có ghi chú | `REVIEW_NOTES_REQUIRED` | 400 |
| `SongListCannotBeRevisedException` | Tạo `version + 1` từ bản không ở Rejected/NeedsRevision | `SONG_LIST_CANNOT_BE_REVISED` | 409 |

## 5. Tham gia & phân công

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `ParticipationAlreadyRespondedException` | Phản hồi khi `Status ≠ Invited` | `PARTICIPATION_ALREADY_RESPONDED` | 409 |
| `ParticipationEventPassedException` | Phản hồi sau giờ sự kiện | `PARTICIPATION_EVENT_PASSED` | 409 |
| `RosterAlreadyFinalizedException` | Sửa/thêm assignment hoặc sinh lại gợi ý khi `Finalized` | `ROSTER_ALREADY_FINALIZED` | 409 |
| `AssignmentDuplicateException` | Cùng ca viên + skill + item bị phân công hai lần trong roster | `ASSIGNMENT_DUPLICATE` | 409 |
| `PersonnelRequiredCountInvalidException` | `RequiredCount <= 0` | `PERSONNEL_REQUIRED_COUNT_INVALID` | 400 |

## 6. Tập hát & bài tập

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `RehearsalTimeInvalidException` | `EndTime <= StartTime` | `REHEARSAL_TIME_INVALID` | 400 |
| `RehearsalAlreadyPassedException` | Sửa buổi tập đã diễn ra | `REHEARSAL_ALREADY_PASSED` | 409 |
| `AttendanceAlreadyRecordedException` | Điểm danh trùng ca viên trong cùng buổi | `ATTENDANCE_ALREADY_RECORDED` | 409 |
| `PracticeDueDateInPastException` | `DueDate < now` khi tạo bài tập | `PRACTICE_DUE_DATE_IN_PAST` | 400 |
| `PracticeTargetRequiredException` | Scope SkillGroup/Individual mà không có target | `PRACTICE_TARGET_REQUIRED` | 400 |
| `PracticeSubmissionPastDueException` | Nộp sau `DueDate` | `PRACTICE_SUBMISSION_PAST_DUE` | 409 |
| `PracticeSubmissionAlreadyReviewedException` | Nhận xét khi `Status ≠ Submitted` | `PRACTICE_SUBMISSION_ALREADY_REVIEWED` | 409 |

## 7. Giao tiếp & hệ thống

| Exception | Khi nào ném | Mã | HTTP |
|---|---|---|---|
| `DirectorNoteTargetRequiredException` | Không có cả `WeekId` lẫn `EventId` | `DIRECTOR_NOTE_TARGET_REQUIRED` | 400 |
| `SettingValueTypeMismatchException` | `Value` không parse được theo `DataType` | `SETTING_VALUE_TYPE_MISMATCH` | 400 |

**Tổng: 37 exception** (+ lớp gốc `DomainException`). Mọi mã đều có trong `error-codes.md`.

`PracticeFeedback.Result` chỉ nhận Passed/NeedsRevision: API chặn bằng FluentValidation
(`VALIDATION_FAILED`); trong entity chỉ cần guard `ArgumentException` (lỗi lập trình,
ra 500) — không có domain exception riêng.

---

## Không phải domain exception (để ở Application)

| Nhóm | Mã | Lý do |
|---|---|---|
| Cần query DB | mọi `*_NOT_FOUND`, `*_DUPLICATE`, `*_ALREADY_EXISTS`, `USER_EMAIL_ALREADY_EXISTS`, `EVENT_SLOT_TAKEN`, `SEASON_DATE_OVERLAP`, `REHEARSAL_TIME_CONFLICT`, `LOOKUP_IN_USE`, `USER_LAST_ADMIN` | Entity không thấy các bản ghi khác |
| Liên quan nhiều aggregate | `SONG_LIST_NOT_LATEST_VERSION`, `SONG_LIST_EVENT_HAS_APPROVED_VERSION`, `ROSTER_SONG_LIST_NOT_APPROVED`, `ROSTER_INSUFFICIENT_MEMBERS`, `ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ASSIGNMENT_MEMBER_NOT_CONFIRMED`, `PRACTICE_NOT_ASSIGNED_TO_MEMBER` | Service điều phối, trả `Result<T>` |
| Validate request / chính sách cấu hình | `MATERIAL_FILE_*`, `PRACTICE_AUDIO_*`, `REPORT_*`, `AUTH_PASSWORD_TOO_WEAK` | FluentValidation hoặc đọc `SystemSetting` |
| Auth, phân quyền, dịch vụ ngoài | `AUTH_*` (trừ refresh token), `NOTIFICATION_NOT_FOR_USER`, `EXTERNAL_*` | Thuộc Application / Infrastructure |

---

## Ghi chú cài đặt

- Luật `AlreadyPassed` / `PastDue` / `Expired` nhận `now` qua tham số, không gọi
  `DateTime.UtcNow` trong entity — để unit test được.
- Đã chốt 2026-09-21: phương án A; `ErrorCodes.cs` đặt ở `Domain/Common`.
  `CLAUDE.md` ("lỗi nghiệp vụ trả qua `Result<T>`") vẫn đúng cho lỗi do Service kiểm tra;
  nên bổ sung một dòng về `DomainException` vào đó.

## Nguồn
- 2026-09-21: tạo từ `harmonia-domain-entity-list.md` và `error-codes.md`.
- 2026-09-21: chốt phương án A; gắn mã `SONG_LIST_CANNOT_BE_REVISED`; bỏ `PracticeFeedbackResultInvalidException`; tách `SkillInactiveException` khỏi `LookupInactiveException` cho khớp `error-codes.md`.

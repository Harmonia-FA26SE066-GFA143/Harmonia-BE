# Harmonia – Report 4 (SDS), mục 3. Detailed Design

Theo template `Report4_Software Design Document`: mỗi feature một mục 3.x, gồm 1 class diagram
và các sequence diagram. Tổng: **14 class diagram, 32 sequence diagram vẽ thật**.

Quy ước:

- **Class diagram**: mỗi feature một sơ đồ, chỉ vẽ các lớp liệt kê trong mục đó.
  Lớp dùng chung (`ApiControllerBase`, `IGenericRepository<T>`, `GenericRepository<T>`, `Result<T>`,
  `INotificationService`, `ICurrentUserService`) vẽ một lần ở 3.1, các mục sau chỉ đặt hộp tên.
  `IChoirScopeService` / `ChoirScopeService` (phạm vi ca đoàn, D8) vẽ đủ ở 3.13; các mục 3.4, 3.6–3.10, 3.12 đặt hộp
  tên và mũi tên phụ thuộc từ service dùng nó (liệt kê trong từng mục).
- **Sequence**
  - `Vẽ` — vẽ riêng.
  - `Tham chiếu <UC>` — cùng cấu trúc với sơ đồ đã vẽ, template cho phép ghi "xem mục …" thay vì vẽ lại.
  - `Tùy chọn` — use case chỉ xem dữ liệu; vẽ ngắn hoặc bỏ.
  - `Trong <UC>` — «include»/«extend», thể hiện bằng khung `opt`/`ref` trong sơ đồ của use case gốc.
- Luồng chung của mọi sequence: Actor → View → Controller → Service → Repository → DbContext.
  Gửi thông báo (S-05) vẽ thành `NotificationService` → `INotificationPublisher` (SignalR).

## 3.1 F1 — Xác thực & Tài khoản cá nhân

**Class:** `AuthController`, `IAuthService`/`AuthService`, `IUserService`/`UserService`, `IUserRepository`,
`IJwtTokenService`, `IPasswordHasherService`, `IEmailSender`, `IGoogleTokenValidator`,
`User`, `Role`, `RefreshToken`, `PasswordResetToken`, `MemberProfile`, cùng các lớp dùng chung (xem quy ước).

| UC | Sơ đồ | Sequence |
|---|---|---|
| S-01 | Login (mật khẩu + Google) | Vẽ |
| S-02 | Logout | Vẽ |
| S-03 | Change / Forgot Password | Vẽ — 2 nhánh: đổi mật khẩu, quên → reset |
| UC-01 | View Personal Profile | Tùy chọn |

## 3.2 F2 — Quản lý Người dùng & Phân quyền

**Class:** `UsersController`, `IUserService`/`UserService`, `IUserRepository`, `IMemberProfileRepository`,
`IPasswordHasherService`, `IEmailSender`, `User`, `Role`, `MemberProfile`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-31 | Manage User Accounts & Roles | Vẽ — tạo tài khoản (sinh mật khẩu, gửi mail) là luồng chính; khoá / gán role là `alt` |

## 3.3 F3 — Quản lý Thành viên & Kỹ năng

**Class:** `MemberProfilesController`, `MemberSkillsController`, `IMemberProfileService`/`MemberProfileService`,
`IMemberSkillService`/`MemberSkillService`, `IMemberProfileRepository`, `IMemberSkillRepository`,
`MemberProfile`, `MemberSkill`, `Skill`, `SkillCategory`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-03 | Declare Skill & Track Approval Status | Vẽ |
| UC-19 | Approve Member-Declared Skills | Vẽ |
| UC-02 | View Assigned Role & Approved Skills | Tùy chọn |
| UC-18 | View Choir Members & Skills | Tùy chọn |

## 3.4 F4 — Lịch Phụng vụ theo Ngày

**Class:** `LiturgicalDaysController`, `LiturgicalEventsController`, `MassSchedulesController`, `ScheduleController`,
`ILiturgicalDayService`/`LiturgicalDayService`, `ILiturgicalEventService`/`LiturgicalEventService`,
`IMassScheduleService`/`MassScheduleService`, `IUpcomingScheduleService`/`UpcomingScheduleService`,
`ILiturgicalDayRepository`, `ILiturgicalEventRepository`, `ILiturgicalCalendarClient`,
`LiturgicalNameTranslationsController`, `ILiturgicalNameTranslationService`/`LiturgicalNameTranslationService`,
`ILiturgicalNameTranslationRepository`, `LiturgicalNamePatterns` (lớp tĩnh), `IUserRepository`,
`LiturgicalDay`, `LiturgicalEvent`, `MassSchedule`, `LiturgicalNameTranslation`, enum `LiturgicalNameKind`.
Hộp tên `IChoirScopeService` ← `LiturgicalEventService`, `UpcomingScheduleService`.
`LiturgicalDayService` phụ thuộc thêm `ILiturgicalNameTranslationRepository` (dịch tên lúc đọc).

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-12 | Manage Daily Liturgical Program | Vẽ — đồng bộ ngày từ API ngoài, tạo sự kiện, công bố (S-05). Người nhận thông báo: `IUserRepository.GetActiveUserIdsOfEventChoirsAsync`; `alt` sự kiện chưa gắn ca đoàn → `GetActiveUserIdsByRolesAsync` (cả giáo xứ). Ngày lễ được sớm hơn mùa một ngày (lễ vọng) |
| UC-12E | Generate Draft Events from Mass Schedules | Trong UC-12 |
| UC-36 | Manage Mass Schedules | Tham chiếu UC-37 |
| (chưa có mã) | Manage Liturgical Name Translations (Admin) | Tham chiếu UC-37 — **chờ chốt**: gộp vào UC-32 hay thêm UC mới |
| UC-04 | View Upcoming Events & Rehearsal Schedule | Tùy chọn |

## 3.5 F5 — Thư viện Thánh nhạc

**Class:** `SongsController`, `MusicMaterialsController`, `ISongService`/`SongService`,
`IMusicMaterialService`/`MusicMaterialService`, `ISongRepository`, `IMusicMaterialRepository`,
`IMaterialLearningProgressRepository`, `IFileStorageService`,
`Song`, `MusicMaterial`, `MaterialLearningProgress`, `SongTheme`, `SongClassification`,
`SongVocalRequirement`, `SongInstrumentRequirement`, record `SongStats`.
`ISongRepository` thêm `IsInUseAsync`, `GetStatsAsync`; `IMusicMaterialService` / `MusicMaterialsController` thêm
`ReplaceFileAsync`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-21 | Manage & Classify Music Library | Vẽ — có upload file (Cloudinary). Xoá bài: `alt` `IsInUseAsync` → `409 SONG_IN_USE`. Thay file tư liệu: upload file mới → lưu → xoá file cũ; lưu lỗi thì xoá file mới; `opt resetProgress` xoá tiến độ học |
| UC-07 | Access Music Materials | Vẽ |
| UC-07E | Search / Filter Materials | Trong UC-07 |
| UC-08 | Mark Material Learning Progress | Vẽ |

## 3.6 F6 — Danh sách Bài hát cho Sự kiện

**Class:** `SongListsController`, `ISongListService`/`SongListService`, `ISongListRepository`,
`SongList`, `SongListItem`, `SongListReview`, `LiturgicalSlot`.
Hộp tên `IChoirScopeService` ← `SongListService` (chỉ ca đoàn phụ trách được sửa, `403 CHOIR_NOT_LEAD`).

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-22 | Manage Song List Lifecycle | Vẽ — đề xuất, gửi duyệt, sửa (tạo phiên bản mới) |
| UC-13 | Review & Approve Song List with Notes | Vẽ |
| UC-06 | View Approved Song List for Event | Tùy chọn |
| UC-14 | View Final Approved Song List | Tùy chọn |

## 3.7 F7 — Lịch tập & Xác nhận Tham gia

**Class:** `RehearsalsController`, `EventParticipationsController`, `IRehearsalService`/`RehearsalService`,
`IRehearsalSongService`/`RehearsalSongService`, `IEventParticipationService`/`EventParticipationService`,
`IRehearsalRepository`, `IEventParticipationRepository`, `Rehearsal`, `RehearsalSong`, `EventParticipation`.
Hộp tên `IChoirScopeService` ← `RehearsalService`, `RehearsalSongService`, `EventParticipationService`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-20 | Schedule Rehearsals | Vẽ |
| UC-23 | Manage Member Participation Confirmation | Vẽ |
| UC-05 | Confirm Event Participation | Vẽ |

## 3.8 F8 — Phân công Phục vụ

**Class:** `ServiceRostersController`, `SongListItemsController`, `IRosterService`/`RosterService`,
`ISongPersonnelRequirementService`/`SongPersonnelRequirementService`, `IServiceRosterRepository`,
`ISongListItemRepository`, `IRosterSuggestionGenerator`, `ISystemSettingService`,
`SongPersonnelRequirement`, `ServiceRoster`, `RosterAssignment`, `RosterShortage`.
Hộp tên `IChoirScopeService` ← `RosterService`, `SongPersonnelRequirementService`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-24 | Define Song Personnel Requirements | Vẽ |
| UC-25 | Request Service Roster Suggestion | Vẽ — gọi Gemini, có luật dự phòng |
| UC-25a | View Shortage Warning | Trong UC-25 |
| UC-25b | Adjust Roster Manually | Trong UC-25 |
| UC-26 | Finalize Service Roster | Vẽ |
| UC-27 | Send Assignment Notifications | Vẽ |

## 3.9 F9 — Bài tập Luyện tập & Chấm điểm

**Class:** `PracticeAssignmentsController`, `PracticeSubmissionsController`,
`IPracticeAssignmentService`/`PracticeAssignmentService`, `IPracticeSubmissionService`/`PracticeSubmissionService`,
`IPracticeAssignmentRepository`, `IPracticeSubmissionRepository`, `IMusicMaterialRepository`, `IFileStorageService`,
`PracticeAssignment`, `PracticeAssignmentTarget`, `PracticeAssignmentMaterial`, `PracticeSubmission`, `PracticeFeedback`.
`PracticeAssignment "1" *-- "*" PracticeAssignmentMaterial`; `MaterialId` trên `PracticeAssignment` giữ lại = tư liệu đầu.
Hộp tên `IChoirScopeService` ← `PracticeAssignmentService`, `PracticeSubmissionService`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-28 | Manage Practice Assignments | Vẽ — kiểm danh sách tư liệu bằng một `IMusicMaterialRepository.ListAsync`, tạo các dòng `PracticeAssignmentMaterial` |
| UC-09 | Receive & Complete Practice Assignment | Vẽ — upload audio |
| UC-29 | Evaluate Practice Submission | Vẽ |
| UC-10 | View Practice Submission Result & Feedback | Vẽ |

## 3.10 F10 — Điểm danh & Theo dõi Chuẩn bị

**Class:** `RehearsalsController`, `LiturgicalEventsController`, `MemberProfilesController`,
`IRehearsalAttendanceService`/`RehearsalAttendanceService`, `IEventPreparationService`/`EventPreparationService`,
`IRehearsalRepository`, `RehearsalAttendance`.
Hộp tên `IChoirScopeService` ← `RehearsalAttendanceService`, `EventPreparationService`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-30 | Take Attendance & Track Progress | Vẽ |
| UC-15 | Monitor Choir Preparation Status | Vẽ |
| UC-11 | View Participation & Practice History | Vẽ |

## 3.11 F11 — Thông báo & Trao đổi

**Class:** `NotificationsController`, `DirectorNotesController`, `NotificationHub`, `INotificationClient`,
`INotificationService`/`NotificationService`, `INotificationPublisher`/`SignalRNotificationPublisher`,
`IDirectorNoteService`/`DirectorNoteService`, `INotificationRepository`, `IDirectorNoteRepository`,
`Notification`, `NotificationRecipient`, `DirectorNote`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| S-04 | View Notifications | Vẽ |
| UC-17 | Send Notes / Requests to Choir Director | Vẽ |
| S-05 | Send Notification | Trong UC-12, 17, 23, 27 |

## 3.12 F12 — Báo cáo & Nhật ký

**Class:** `ReportsController`, `AuditLogsController`, `IReportService`/`ReportService`,
`IReportExportService`/`ReportExportService`, `IAuditLogService`/`AuditLogService`, `IReportExportRepository`,
`IAuditLogRepository`, `IReportFileWriter`, `IFileStorageService`, `ReportExport`, `AuditLog`,
lớp tĩnh `ReportExportColumns`, enum `ReportPeriod`.
`IReportService` thêm `GetActivityTrendAsync`; `IReportExportService` thêm `GetColumns`; `ReportsController` thêm
`GetActivityTrendAsync`, `GetColumns`. Hộp tên `IChoirScopeService` ← `ReportService`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-34 | View Reports | Vẽ — thêm `activity-trend`: đọc mọi trang ca viên → bài tập theo hạn → chia kỳ tuần / tháng / năm |
| UC-34E | Export Reports | Trong UC-34 |
| UC-16 | View Service & Song Usage Reports | Tùy chọn |
| UC-35 | View Activity History / Audit Log | Tùy chọn |

## 3.13 F13 — Quản lý Ca đoàn

**Class:** `ChoirsController`, `LiturgicalEventsController`, `IChoirService`/`ChoirService`,
`IGenericRepository<Choir>`, `IGenericRepository<ChoirMembership>`, `IGenericRepository<LiturgicalEventChoir>`,
`Choir`, `ChoirMembership`, `LiturgicalEventChoir`, `IChoirScopeService`/`ChoirScopeService` (vẽ đủ ở đây).
`Choir` có `DirectorId` (D8, Admin gán) → quan hệ `User "0..1" -- "*" Choir`; `LiturgicalEventChoir` có `IsLead`;
`UpdateEventChoirsRequest` có `leadChoirId`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-37 | Manage Choirs | Vẽ — **sơ đồ CRUD mẫu**, các mục tham chiếu tới đây. Chỉ Admin tạo / sửa và gán ca trưởng |
| UC-38 | Manage Choir Membership | Vẽ — ca trưởng chỉ thao tác trên ca đoàn của mình (ca đoàn khác → `404 CHOIR_NOT_FOUND`) |
| UC-39 | Assign Choirs to Event | Vẽ — chỉ ParishPriest; chọn ca đoàn phụ trách (`leadChoirId`) |

## 3.14 F14 — Cấu hình Hệ thống

**Class:** `LookupsController`, `SystemSettingsController`, `ILookupService`/`LookupService`,
`ISystemSettingService`/`SystemSettingService`, `IGenericRepository<T>`,
`SkillCategory`, `Skill`, `LiturgicalSeason`, `MassType`, `CeremonyType`, `EventCategory`,
`WorshipLocation`, `LiturgicalSlot`, `SongTheme`, `SystemSetting`.
`ISystemSettingService.GetPublicAsync(bool isSignedIn, …)` (endpoint `public` không cần đăng nhập);
`SystemSettingDto` thêm `DisplayName`, `Group`, `Order`.

| UC | Sơ đồ | Sequence |
|---|---|---|
| UC-32 | Configure System Categories | Tham chiếu UC-37 |
| UC-33 | Manage System Settings | Tham chiếu UC-37 |

## Tổng số sequence

| Loại | Số | Use case |
|---|---|---|
| Vẽ | 32 | còn lại |
| Tham chiếu | 3 | UC-32, UC-33, UC-36 → UC-37 |
| Tùy chọn | 8 | UC-01, 02, 04, 06, 14, 16, 18, 35 |
| Trong sơ đồ gốc | 6 | S-05, UC-07E, UC-12E, UC-25a, UC-25b, UC-34E |

Tổng 49 use case = 32 + 3 + 8 + 6. Chưa tính "Manage Liturgical Name Translations" (3.4) cho tới khi chốt mã UC.

## Lịch sử sửa

**2026-10-26** — đối chiếu `main@718696c` (D8 và đợt backlog Web), so với bản trước:

- **Thêm** quy ước: `IChoirScopeService` / `ChoirScopeService` vẽ đủ ở 3.13, các mục khác đặt hộp tên + mũi tên phụ
  thuộc; thêm dòng "Hộp tên `IChoirScopeService` ← …" ở 3.4, 3.6, 3.7, 3.8, 3.9, 3.10, 3.12.
- **Thêm** class: 3.4 cụm `LiturgicalNameTranslation…`, `LiturgicalNamePatterns`, `IUserRepository`, enum
  `LiturgicalNameKind`; 3.5 `SongStats`; 3.9 `PracticeAssignmentMaterial`, `IMusicMaterialRepository`;
  3.12 `ReportExportColumns`, enum `ReportPeriod`.
- **Thêm** method mới cần thể hiện: 3.5 `IsInUseAsync`, `GetStatsAsync`, `ReplaceFileAsync`; 3.12
  `GetActivityTrendAsync`, `GetColumns`; 3.14 `GetPublicAsync(bool isSignedIn)`, 3 trường mới của `SystemSettingDto`.
- **Thêm** 3.13: `Choir.DirectorId`, `LiturgicalEventChoir.IsLead`, `leadChoirId`; ghi chú quyền D8 vào UC-37, 38, 39.
- **Thêm** dòng use case "Manage Liturgical Name Translations" ở 3.4, chưa có mã, chờ chốt.
- **Sửa** ghi chú sequence UC-12 (người nhận thông báo theo ca đoàn phục vụ, lễ vọng sớm một ngày), UC-21 (chặn xoá
  bài đã dùng, thay file tư liệu), UC-28 (nhiều tư liệu), UC-34 (`activity-trend`).
- **Xoá** không có. Tổng số sequence giữ 32.

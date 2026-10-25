# Từ điển nghiệp vụ — dùng đúng tên, không tự đặt từ đồng nghĩa

Nguồn chuẩn: `doc/harmonia-domain-entity-list.md` (48 entity, 22 enum).
Tên nào không có ở đây thì tra file đó, đừng tự dịch.

| Tiếng Việt | Tên trong code |
|---|---|
| tài khoản | `User` |
| ca đoàn | `Choir` |
| ca viên thuộc ca đoàn | `ChoirMembership` |
| ca đoàn phục vụ sự kiện | `LiturgicalEventChoir` |
| token đặt lại mật khẩu | `PasswordResetToken` |
| vai trò | `Role` — 4 giá trị: Admin, ParishPriest, ChoirDirector, ChoirMember |
| cha xứ / ban phụng vụ | `ParishPriest` |
| ca trưởng | `ChoirDirector` — KHÔNG dùng `ChoirLeader` |
| ca viên | `ChoirMember` (role) / `MemberProfile` (hồ sơ) |
| nhạc công | KHÔNG có role riêng — là `ChoirMember` có `MemberSkill` thuộc `SkillCategory` = Instrument |
| nhóm kỹ năng | `SkillCategory` — Vocal, Instrument, Solo, Psalm, Conducting support |
| kỹ năng | `Skill` — Soprano, Alto, Tenor, Bass, Guitar, Organ, Solo, Psalmist |
| kỹ năng đã khai báo | `MemberSkill` |
| ngày phụng vụ (cache lịch Công giáo từ API ngoài) | `LiturgicalDay` — KHÔNG còn `LiturgicalWeek`, lịch lên theo ngày (D6) |
| sự kiện / thánh lễ | `LiturgicalEvent` — KHÔNG dùng `Event` (trùng từ khoá C#) |
| mùa phụng vụ | `LiturgicalSeason` — KHÔNG dùng `Season` |
| loại thánh lễ | `MassType` |
| loại nghi thức | `CeremonyType` |
| loại sự kiện | `EventCategory` |
| địa điểm cử hành | `WorshipLocation` |
| giờ lễ cố định của giáo xứ | `MassSchedule` |
| bài hát | `Song` |
| chủ đề bài hát | `SongTheme` |
| phân loại bài hát | `SongClassification` |
| tài liệu âm nhạc | `MusicMaterial` — bản nhạc là `MaterialType.SheetMusic`, KHÔNG có entity `SheetMusic` |
| tiến độ học | `MaterialLearningProgress` |
| danh sách bài hát | `SongList` (một dòng một phiên bản) |
| bài trong danh sách | `SongListItem` |
| vị trí phụng vụ | `LiturgicalSlot` — Nhập lễ, Đáp ca, Dâng lễ, Hiệp lễ, Kết lễ |
| quyết định duyệt | `SongListReview` |
| xác nhận tham gia | `EventParticipation` — KHÔNG dùng `ParticipationConfirmation` |
| yêu cầu nhân sự cho bài | `SongPersonnelRequirement` |
| phân công phục vụ | `ServiceRoster` — KHÔNG dùng `Assignment` |
| dòng phân công | `RosterAssignment` |
| cảnh báo thiếu người | `RosterShortage` |
| buổi tập | `Rehearsal` |
| điểm danh | `RehearsalAttendance` — KHÔNG dùng `Attendance` trần |
| bài hát của buổi tập | `RehearsalSong` — KHÔNG phải `SongList` (không phiên bản, không duyệt) |
| bài tập về nhà | `PracticeAssignment` |
| bản thu nộp | `PracticeSubmission` |
| nhận xét của ca trưởng | `PracticeFeedback` — KHÔNG dùng `Feedback` trần |
| thông báo | `Notification` / `NotificationRecipient` |
| ghi chú cha xứ gửi ca trưởng | `DirectorNote` |
| cấu hình hệ thống | `SystemSetting` |
| lịch sử hoạt động | `AuditLog` |

**Nhiều ca đoàn (D7, chốt 2026-10-17):** ca viên gia nhập nhiều `Choir` qua `ChoirMembership`, sự kiện có nhiều `Choir` qua `LiturgicalEventChoir`. Mỗi `Choir` có **một** ca trưởng (`Choir.DirectorId`, Admin gán); một `ChoirDirector` phụ trách được nhiều ca đoàn và chỉ quản lý ca đoàn của mình — ca đoàn của người khác trả 404 (D8, chốt 2026-10-25). Sự kiện nhiều ca đoàn do **ca đoàn phụ trách** (`LiturgicalEventChoir.IsLead`, cha xứ chọn) lập danh sách bài, mời và phân công; ca đoàn phục vụ khác chỉ xem. Mọi service kiểm phạm vi qua `IChoirScopeService`; bản ghi chưa gắn ca đoàn thì ca trưởng nào cũng quản lý được.

`LiturgicalEvent`, `MemberProfile`, `Song`, `Skill` là bốn hub — hầu hết entity khác
móc vào chúng.

## Quy ước chung

- Một file một class. Tên file trùng tên class.
- Thuộc tính boolean bắt đầu bằng `Is` / `Has` / `Can` — `IsActive`, `IsMandatory`.
- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.
  Ngoại lệ: method có chữ ký do framework quy định (middleware `InvokeAsync`, filter,
  `Hub`, `BackgroundService`) không thêm được token. Lấy token từ `HttpContext.RequestAborted`
  hoặc `stoppingToken` rồi truyền xuống các lời gọi bên trong.
  Trong `test/` (xUnit v3): method `[Fact]` / `[Theory]` giữ hậu tố `_Async`
  (`Login_ValidCredentials_ReturnsTokensAndUser_Async`). xUnit không cho method test nhận token
  làm tham số, nên lấy từ field `_ct = TestContext.Current.CancellationToken` rồi truyền xuống
  mọi lời gọi — analyzer `xUnit1051` cảnh báo nếu quên. Helper async trong test vẫn phải có
  `CancellationToken cancellationToken = default` (chốt 2026-09-27).

- Mọi chuỗi trong `.cs` là tiếng Anh — comment, XML doc, message exception, message log.
  Message lỗi cho người dùng đi bằng mã lỗi, không bằng câu tiếng Việt.
  Ngoại lệ: `Title` / `Content` của `SendNotificationRequest` viết tiếng Việt ngay trong service
  (và chuỗi test so khớp chúng) — thông báo lưu DB rồi hiển thị nguyên văn cho người dùng,
  không có mã để FE dịch (chốt 2026-10-11). Comment, log, exception quanh đó vẫn tiếng Anh.
  Ngoại lệ: `ReportExportColumns.cs` giữ tên báo cáo và tiêu đề cột tiếng Việt của file xuất
  (CSV, XLSX, PDF) — file đi thẳng tới Admin, không qua FE (chốt 2026-10-11). Chuỗi tiếng Việt
  của file xuất chỉ được nằm trong file này.
  Ngoại lệ: dữ liệu seed hiển thị cho người dùng viết tiếng Việt — `Name` trong `HasData` của
  `SkillCategoryConfiguration`, `Description` trong `SystemSettingKeys`, `FullName` của Admin đầu tiên
  trong `DataSeeder` (chốt 2026-10-17). Tên role và key của setting vẫn tiếng Anh — code so khớp theo chúng.

## Domain

- Entity: danh từ số ít — `SongList`, `LiturgicalEvent`.
- Enum: số ít, KHÔNG có hậu tố `Enum` — `SongListStatus`, `ApprovalStatus`.
- Value object: danh từ, không hậu tố — `TimeSlot`, `ContactInfo`.
- Exception nghiệp vụ: `<Nội dung>Exception`, kế thừa `DomainException`, truyền mã từ `ErrorCodes` — `SongListAlreadyApprovedException`.

## Application

DTO chia ba nhóm theo hướng đi của dữ liệu:

- Trả ra client: `<Entity>Dto` — `SongListDto`, `NotificationDto`.
  Cần tách chi tiết / tóm tắt: `<Entity>DetailDto`, `<Entity>SummaryDto`.
- Nhận từ client: `<Hành động><Entity>Request` — `CreateSongListRequest`,
  `UpdateSongListRequest`, `ApproveSongListRequest`.
- Chỉ đặt `<X>Response` khi payload trả về KHÔNG phải một entity —
  `LoginResponse`, `RosterSuggestionResponse`.

Không trả entity ra controller, không nhận entity làm tham số action.

- Interface repository: `I<Entity>Repository` — `ISongListRepository`.
  Repository dùng chung: `IGenericRepository<T>` (`GetByIdAsync`, `AddAsync`, `Remove`,
  `SaveChangesAsync`). Entity chỉ cần CRUD đơn giản thì inject thẳng `IGenericRepository<Song>`,
  không tạo repository riêng. Cần truy vấn đặc thù thì tạo
  `I<Entity>Repository : IGenericRepository<Entity>` và chỉ khai thêm method đặc thù.
- Interface service: `I<Nghiệp vụ>Service` — `ISongListService`, `IRosterService`.
- Service: bỏ chữ `I` của interface tương ứng — `SongListService`, `AuthService`.
- Validator: `<Request>Validator` — `CreateSongListRequestValidator`.
- AutoMapper profile: `<Entity>Profile`. Một profile một entity.
- Exception ứng dụng: `<X>Exception` — `NotFoundException`, `ValidationException`.
- Service trả `Result` / `Result<T>`; danh sách trả `PagedList<T>`.
- Service không bắt `DomainException`.

## Mã lỗi

- SCREAMING_SNAKE, dạng `<ĐỐI_TƯỢNG>_<LÝ_DO>` — `SONG_LIST_ALREADY_APPROVED`.
- Khai thành hằng trong `Domain/Common/ErrorCodes.cs`, không gõ chuỗi trực tiếp.
- Validation dùng `.WithErrorCode(ErrorCodes.X)` của FluentValidation.
- `Result<T>` và `DomainException` mang mã lỗi, không mang câu tiếng Việt.
- Danh mục đầy đủ và bản dịch: `doc/error-codes.md`. Thêm mã mới phải cập nhật
  file đó trong cùng PR, nếu không FE hiển thị mã thô cho người dùng.

## Infrastructure

- Repository: `<Entity>Repository`, kế thừa `GenericRepository<Entity>` — dùng property
  `DbContext` của lớp cha, không giữ tham số `dbContext` riêng.
- EF configuration: `<Entity>Configuration`.
- `DbSet` đặt tên số nhiều — `public DbSet<SongList> SongLists`.
- Interceptor: `<X>Interceptor`. External service đặt theo thứ nó bọc —
  `EmailSender`, `JwtTokenService`.

## API

- Controller: `<Entity số nhiều>Controller` — `SongListsController`, `MemberSkillsController`.
- Route kebab-case số nhiều: `api/song-lists`, `api/liturgical-events`, `api/member-skills`.
  KHÔNG dùng `api/SongList`.
  Luật số nhiều chỉ áp cho route đại diện một tập bản ghi. Nhóm endpoint không phải
  tài nguyên thì giữ danh từ số ít: `api/auth` (chốt 2026-09-22 — `api/auths` vô nghĩa,
  còn đổi sang `api/sessions` thì lệch khỏi `AuthController`/`AuthService`).
  `api/schedule` cũng vậy (chốt 2026-10-13): nhóm xem tổng hợp sự kiện + buổi tập sắp tới,
  không có entity `Schedule`; `ScheduleController` giữ số ít theo route.
- Middleware: `<X>Middleware`. Filter: `<X>Filter`. Extension: `<X>Extensions`.
- Hub: `<X>Hub` + interface client `I<X>Client` — `NotificationHub`, `INotificationClient`.
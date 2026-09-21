# Từ điển nghiệp vụ — dùng đúng tên, không tự đặt từ đồng nghĩa

Nguồn chuẩn: `doc/harmonia-domain-entity-list.md` (42 entity, 21 enum).
Tên nào không có ở đây thì tra file đó, đừng tự dịch.

| Tiếng Việt | Tên trong code |
|---|---|
| tài khoản | `User` |
| vai trò | `Role` — 4 giá trị: Admin, ParishPriest, ChoirDirector, ChoirMember |
| cha xứ / ban phụng vụ | `ParishPriest` |
| ca trưởng | `ChoirDirector` — KHÔNG dùng `ChoirLeader` |
| ca viên | `ChoirMember` (role) / `MemberProfile` (hồ sơ) |
| nhạc công | KHÔNG có role riêng — là `ChoirMember` có `MemberSkill` thuộc `SkillCategory` = Instrument |
| nhóm kỹ năng | `SkillCategory` — Vocal, Instrument, Solo, Psalm, Conducting support |
| kỹ năng | `Skill` — Soprano, Alto, Tenor, Bass, Guitar, Organ, Solo, Psalmist |
| kỹ năng đã khai báo | `MemberSkill` |
| tuần phụng vụ | `LiturgicalWeek` |
| sự kiện / thánh lễ | `LiturgicalEvent` — KHÔNG dùng `Event` (trùng từ khoá C#) |
| mùa phụng vụ | `LiturgicalSeason` — KHÔNG dùng `Season` |
| loại thánh lễ | `MassType` |
| loại nghi thức | `CeremonyType` |
| loại sự kiện | `EventCategory` |
| địa điểm cử hành | `WorshipLocation` |
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
| bài tập về nhà | `PracticeAssignment` |
| bản thu nộp | `PracticeSubmission` |
| nhận xét của ca trưởng | `PracticeFeedback` — KHÔNG dùng `Feedback` trần |
| thông báo | `Notification` / `NotificationRecipient` |
| ghi chú cha xứ gửi ca trưởng | `DirectorNote` |
| cấu hình hệ thống | `SystemSetting` |
| lịch sử hoạt động | `AuditLog` |

**Không có entity `Choir`.** Hệ thống phục vụ một ca đoàn của một giáo xứ, chỉ có nhiều
`WorshipLocation`. Đừng viết code dựa trên giả định nhiều ca đoàn.

`LiturgicalEvent`, `MemberProfile`, `Song`, `Skill` là bốn hub — hầu hết entity khác
móc vào chúng.

## Quy ước chung

- Một file một class. Tên file trùng tên class.
- Thuộc tính boolean bắt đầu bằng `Is` / `Has` / `Can` — `IsActive`, `IsMandatory`.
- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.
  Ngoại lệ: method có chữ ký do framework quy định (middleware `InvokeAsync`, filter,
  `Hub`, `BackgroundService`) không thêm được token. Lấy token từ `HttpContext.RequestAborted`
  hoặc `stoppingToken` rồi truyền xuống các lời gọi bên trong.

- Mọi chuỗi trong `.cs` là tiếng Anh — comment, XML doc, message exception, message log.
  Không có ngoại lệ: message cho người dùng đi bằng mã lỗi, không bằng câu tiếng Việt.

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

- Repository: `<Entity>Repository`.
- EF configuration: `<Entity>Configuration`.
- `DbSet` đặt tên số nhiều — `public DbSet<SongList> SongLists`.
- Interceptor: `<X>Interceptor`. External service đặt theo thứ nó bọc —
  `EmailSender`, `JwtTokenService`.

## API

- Controller: `<Entity số nhiều>Controller` — `SongListsController`, `MemberSkillsController`.
- Route kebab-case số nhiều: `api/song-lists`, `api/liturgical-events`, `api/member-skills`.
  KHÔNG dùng `api/SongList`.
- Middleware: `<X>Middleware`. Filter: `<X>Filter`. Extension: `<X>Extensions`.
- Hub: `<X>Hub` + interface client `I<X>Client` — `NotificationHub`, `INotificationClient`.
# Từ điển nghiệp vụ — dùng đúng tên, không tự đặt từ đồng nghĩa

| Tiếng Việt | Tên trong code |
|---|---|
| cha xứ / ban phụng vụ | `ParishPriest` |
| ca trưởng | `ChoirDirector` — KHÔNG dùng `ChoirLeader` |
| ca viên | `ChoirMember` |
| nhạc công | `Instrumentalist` |
| ca đoàn | `Choir` |
| sự kiện / thánh lễ | `LiturgicalEvent` — KHÔNG dùng `Event` (trùng từ khoá C#) |
| mùa phụng vụ | `LiturgicalSeason` — KHÔNG dùng `Season` |
| loại thánh lễ | `MassType` |
| nghi thức (hôn phối, an táng...) | `Ceremony` |
| danh sách bài hát | `SongList` |
| yêu cầu nhân sự cho bài | `SongPersonnelRequirement` |
| phân công phục vụ | `ServiceRoster` — KHÔNG dùng `Assignment` |
| xác nhận tham gia | `ParticipationConfirmation` |
| buổi tập | `Rehearsal` |
| bài tập về nhà | `PracticeAssignment` |
| bản thu nộp | `PracticeSubmission` |
| nhận xét của ca trưởng | `Feedback` |
| điểm danh | `Attendance` |
| bản nhạc | `SheetMusic` |
| bè | `VocalPart` |
| kỹ năng | `Skill` — Soprano, Alto, Tenor, Bass, Psalmist, Solo, Guitar, Organ |

`LiturgicalEvent` là entity trung tâm — lịch tập, danh sách bài hát, phân công phục vụ
và xác nhận tham gia đều móc vào nó.

Gặp thuật ngữ nghiệp vụ chưa có trong bảng: hỏi, đừng tự dịch.

## Quy ước chung

- Một file một class. Tên file trùng tên class.
- Thuộc tính boolean bắt đầu bằng `Is` / `Has` / `Can` — `IsApproved`, `HasShortage`.
- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.

## Domain

- Entity: danh từ số ít — `SongList`, `LiturgicalEvent`.
- Enum: số ít, KHÔNG có hậu tố `Enum` — `SongListStatus`, `SkillLevel`.
- Value object: danh từ, không hậu tố — `TimeSlot`, `ContactInfo`.
- Exception nghiệp vụ: `<Nội dung>Exception` — `SongListAlreadyApprovedException`.

## Application

DTO chia ba nhóm theo hướng đi của dữ liệu:

- Trả ra client: `<Entity>Dto` — `SongListDto`, `NotificationDto`.
  Bản chi tiết / tóm tắt khi cần tách: `<Entity>DetailDto`, `<Entity>SummaryDto`.
- Nhận từ client: `<Hành động><Entity>Request` — `CreateSongListRequest`,
  `UpdateSongListRequest`, `ApproveSongListRequest`.
- Chỉ đặt `<X>Response` khi payload trả về KHÔNG phải một entity —
  `LoginResponse`, `RosterSuggestionResponse`.

Không bao giờ trả entity thẳng ra controller.

- Interface repository: `I<Entity>Repository` — `ISongListRepository`.
- Interface service: `I<Nghiệp vụ>Service` — `ISongListService`, `IRosterService`.
- Service: bỏ chữ `I` của interface tương ứng — `SongListService`, `AuthService`.
- Validator: `<Request>Validator` — `CreateSongListRequestValidator`.
- AutoMapper profile: `<Entity>Profile` — `SongListProfile`. Một profile một entity,
  không gom hết vào một `MappingProfile`.
- Exception ứng dụng: `<X>Exception` — `NotFoundException`, `ValidationException`.
- Service trả `Result<T>`; danh sách trả `PagedList<T>`.

## Infrastructure

- Repository: `<Entity>Repository` — `SongListRepository`.
- EF configuration: `<Entity>Configuration` — `LiturgicalEventConfiguration`.
- `DbSet` đặt tên số nhiều — `public DbSet<SongList> SongLists`.
- Interceptor: `<X>Interceptor`. External service: tên công nghệ nó bọc —
  `EmailSender`, `JwtTokenService`.

## API

- Controller: `<Entity số nhiều>Controller` — `SongListsController`, `ChoirMembersController`.
- Route kebab-case số nhiều: `api/song-lists`, `api/liturgical-events`,
  `api/choir-members`. KHÔNG dùng `api/SongList`.
- Middleware: `<X>Middleware`. Filter: `<X>Filter`. Extension: `<X>Extensions`.
- Hub: `<X>Hub` + interface client `I<X>Client` — `NotificationHub`, `INotificationClient`.
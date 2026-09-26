# Từ điển nghiệp vụ — dùng đúng tên, không tự đặt từ đồng nghĩa

| Tiếng Việt | Tên trong code |
|---|---|
| cha xứ / ban phụng vụ | `ParishPriest` |
| ca trưởng | `ChoirDirector` — KHÔNG dùng `ChoirLeader` |
| ca viên | `ChoirMember` (role) · `MemberProfile` (hồ sơ) |
| nhạc công | KHÔNG có role riêng — là `ChoirMember` có `MemberSkill` thuộc `SkillCategory` nhạc cụ |
| tuần phụng vụ | `LiturgicalWeek` |
| sự kiện / thánh lễ | `LiturgicalEvent` — KHÔNG dùng `Event` (trùng từ khoá C#) |
| mùa phụng vụ | `LiturgicalSeason` — KHÔNG dùng `Season` |
| loại thánh lễ | `MassType` |
| nghi thức (hôn phối, an táng...) | `CeremonyType` |
| nơi cử hành | `WorshipLocation` |
| vị trí bài trong lễ | `LiturgicalSlot` |
| bài hát | `Song` |
| tư liệu bài hát (bản nhạc, lời, audio mẫu) | `MusicMaterial` + enum `MaterialType` |
| danh sách bài hát | `SongList` — mỗi phiên bản một dòng |
| bài trong danh sách | `SongListItem` |
| cha xứ duyệt danh sách | `SongListReview` |
| yêu cầu nhân sự cho bài | `SongPersonnelRequirement` |
| phân công phục vụ | `ServiceRoster` (cả bảng) · `RosterAssignment` (một dòng phân công) |
| xác nhận tham gia | `EventParticipation` |
| buổi tập | `Rehearsal` |
| điểm danh | `RehearsalAttendance` |
| bài tập về nhà | `PracticeAssignment` |
| bản thu nộp | `PracticeSubmission` |
| nhận xét của ca trưởng | `PracticeFeedback` |
| thông báo | `Notification` + `NotificationRecipient` |
| kỹ năng | `Skill` · nhóm `SkillCategory` · kỹ năng của ca viên `MemberSkill` |

**Hệ thống phục vụ MỘT ca đoàn.** Không có entity `Choir`, không có `ChoirId` ở đâu cả.

`LiturgicalEvent` là entity trung tâm — lịch tập, danh sách bài hát, phân công phục vụ
và xác nhận tham gia đều móc vào nó. Ba hub còn lại: `MemberProfile`, `Song`, `Skill`.

Nguồn chuẩn đầy đủ 42 entity + 21 enum: `doc/harmonia-domain-entity-list.md`.
Bảng trên chỉ là lối vào nhanh. Gặp thuật ngữ chưa có ở cả hai chỗ: hỏi, đừng tự dịch.

## Quy ước tên khác

- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.
  Ngoại lệ: method có chữ ký do framework quy định (middleware `InvokeAsync`, filter,
  `Hub`, `BackgroundService`) không thêm được token. Lấy token từ `HttpContext.RequestAborted`
  hoặc `stoppingToken` rồi truyền xuống các lời gọi bên trong.

- Interface repository: `I<Entity>Repository` — `ISongListRepository`, kế thừa
  `IGenericRepository<Entity>`. CRUD đơn giản thì inject thẳng `IGenericRepository<T>`.
- Service: `<Nghiệp vụ>Service` — `SongListService`, `RosterService`, `AuthService`.
- EF configuration: `<Entity>Configuration` — `LiturgicalEventConfiguration`.
- Validator: `<Request>Validator` — `CreateSongListRequestValidator`.
- Service trả `Result<T>`; danh sách trả `PagedList<T>`.
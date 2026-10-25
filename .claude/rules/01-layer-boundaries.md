# Từ điển nghiệp vụ — dùng đúng tên, không tự đặt từ đồng nghĩa

| Tiếng Việt | Tên trong code |
|---|---|
| cha xứ / ban phụng vụ | `ParishPriest` |
| ca trưởng | `ChoirDirector` — KHÔNG dùng `ChoirLeader` |
| ca viên | `ChoirMember` (role) · `MemberProfile` (hồ sơ) |
| nhạc công | KHÔNG có role riêng — là `ChoirMember` có `MemberSkill` thuộc `SkillCategory` nhạc cụ |
| ngày phụng vụ (cache lịch Công giáo từ API ngoài) | `LiturgicalDay` — KHÔNG còn `LiturgicalWeek`, lịch lên theo ngày (D6) |
| sự kiện / thánh lễ | `LiturgicalEvent` — KHÔNG dùng `Event` (trùng từ khoá C#) |
| mùa phụng vụ | `LiturgicalSeason` — KHÔNG dùng `Season` |
| loại thánh lễ | `MassType` |
| nghi thức (hôn phối, an táng...) | `CeremonyType` |
| nơi cử hành | `WorshipLocation` |
| giờ lễ cố định của giáo xứ | `MassSchedule` |
| vị trí bài trong lễ | `LiturgicalSlot` |
| ca đoàn | `Choir` · thành viên ca đoàn `ChoirMembership` · ca đoàn phục vụ sự kiện `LiturgicalEventChoir` |
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
| bài hát của buổi tập | `RehearsalSong` — KHÔNG phải `SongList` (không phiên bản, không duyệt) |
| bài tập về nhà | `PracticeAssignment` |
| bản thu nộp | `PracticeSubmission` |
| nhận xét của ca trưởng | `PracticeFeedback` |
| thông báo | `Notification` + `NotificationRecipient` |
| kỹ năng | `Skill` · nhóm `SkillCategory` · kỹ năng của ca viên `MemberSkill` |

**Nhiều ca đoàn (D7, chốt 2026-10-17):** ca viên gia nhập nhiều `Choir` qua `ChoirMembership`, sự kiện có nhiều `Choir` qua `LiturgicalEventChoir`. Mỗi `Choir` có **một** ca trưởng (`Choir.DirectorId`, Admin gán); một `ChoirDirector` phụ trách được nhiều ca đoàn và chỉ quản lý ca đoàn của mình — ca đoàn của người khác trả 404 (D8, chốt 2026-10-25). Sự kiện nhiều ca đoàn do **ca đoàn phụ trách** (`LiturgicalEventChoir.IsLead`, cha xứ chọn) lập danh sách bài, mời và phân công; ca đoàn phục vụ khác chỉ xem. Mọi service kiểm phạm vi qua `IChoirScopeService`; bản ghi chưa gắn ca đoàn thì ca trưởng nào cũng quản lý được.

`LiturgicalEvent` là entity trung tâm — lịch tập, danh sách bài hát, phân công phục vụ
và xác nhận tham gia đều móc vào nó. Ba hub còn lại: `MemberProfile`, `Song`, `Skill`.

Nguồn chuẩn đầy đủ 48 entity + 22 enum: `doc/harmonia-domain-entity-list.md`.
Bảng trên chỉ là lối vào nhanh. Gặp thuật ngữ chưa có ở cả hai chỗ: hỏi, đừng tự dịch.

## Quy ước tên khác

- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.
  Ngoại lệ: method có chữ ký do framework quy định (middleware `InvokeAsync`, filter,
  `Hub`, `BackgroundService`) không thêm được token. Lấy token từ `HttpContext.RequestAborted`
  hoặc `stoppingToken` rồi truyền xuống các lời gọi bên trong.
  Trong `test/`: token lấy từ `TestContext.Current.CancellationToken` (chi tiết ở `02-naming.md`).

- Interface repository: `I<Entity>Repository` — `ISongListRepository`, kế thừa
  `IGenericRepository<Entity>`. CRUD đơn giản thì inject thẳng `IGenericRepository<T>`.
- Service: `<Nghiệp vụ>Service` — `SongListService`, `RosterService`, `AuthService`.
- EF configuration: `<Entity>Configuration` — `LiturgicalEventConfiguration`.
- Validator: `<Request>Validator` — `CreateSongListRequestValidator`.
- Service trả `Result<T>`; danh sách trả `PagedList<T>`.
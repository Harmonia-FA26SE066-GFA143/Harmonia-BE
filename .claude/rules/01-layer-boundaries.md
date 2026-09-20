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
| phân công phục vụ | `ServiceRoster` — KHÔNG dùng `Assignment` |
| yêu cầu nhân sự cho bài | `SongPersonnelRequirement` |
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

## Quy ước tên khác

- Async toàn bộ, hậu tố `Async`, luôn truyền `CancellationToken`.
- Interface repository: `I<Entity>Repository` — `ISongListRepository`.
- Service: `<Nghiệp vụ>Service` — `SongListService`, `RosterService`, `AuthService`.
- EF configuration: `<Entity>Configuration` — `LiturgicalEventConfiguration`.
- Validator: `<Request>Validator` — `CreateSongListRequestValidator`.
- Service trả `Result<T>`; danh sách trả `PagedList<T>`.
# Changelog hợp đồng API

Những thay đổi Web và Mobile cần biết, mới nhất ở trên. Chi tiết từng endpoint ở `api.md`, mã lỗi ở `error-codes.md`.

## Quy ước

- **Thay đổi phá vỡ** (breaking): bỏ hoặc đổi tên endpoint / trường, đổi kiểu hay ý nghĩa của trường, thêm trường
  **bắt buộc** vào request, siết quyền (role cũ bị 403/404), đổi mã lỗi đang trả. Ghi rõ **[Phá vỡ]** và báo nhóm FE
  trước khi merge.
- **Không phá vỡ**: thêm endpoint, thêm trường vào response, thêm trường tuỳ chọn vào request, thêm mã lỗi mới cho tình
  huống trước đây chưa có. FE bỏ qua trường lạ, hiển thị câu chung cho mã lạ.
- Mỗi PR đổi hợp đồng thêm một dòng vào mục ngày tương ứng ở đây, cùng PR cập nhật `api.md` / `error-codes.md`.

## 2026-11-10

Karaoke và bản nhạc chạy theo nốt (yêu cầu Web). Cần migration thêm bảng `TimedLyrics` và cột `FileRevision`,
`AudioDurationSeconds`, `PlaybackNotesSha256` vào `MusicMaterials` trước khi deploy.

### Không phá vỡ

- Endpoint mới `GET | PUT | DELETE api/music-materials/{id}/timed-lyrics` → `TimedLyricsDto` (`api.md` mục 7).
  Mã mới: `TIMED_LYRICS_NOT_FOUND`, `TIMED_LYRICS_EXCEEDS_DURATION`, `TIMED_LYRICS_LINES_INVALID`, `MATERIAL_NOT_AUDIO`,
  `MATERIAL_NOT_LYRICS`.
- `SongDto` (`GET api/songs`, `GET api/songs/{id}`, POST/PUT) thêm `sheetMusicThumbnailUrl: string | null` — ảnh nhỏ
  trang 1 bản nhạc cho danh sách bài hát, URL không hết hạn. Không cần migration.
- `MusicMaterialDto` thêm `fileRevision` (1 khi upload, +1 mỗi lần thay file).
- `InteractiveScoreDto` thêm `notesUrl`, `notesSha256`, `notesSchemaVersion` — một response đủ score + WAV + notes của
  cùng revision.
- `notes.json` của score mới upload / thay file là schema v2: mỗi nốt có `id`, `partId`, `measureIndex`, `voice`,
  `staff`, `noteIndex`, thêm `measures[]` và `readingOrder`. Vẫn đủ trường v1. `PlaybackDto.schemaVersion` = 2 cho
  các score này. File MIDI vẫn v1.

### [Phá vỡ] Upload audio hỏng

- `POST api/music-materials` và `PUT {id}/file` với `.mp3` / `.m4a` / `.wav` mà server không đọc được độ dài → 422
  `MATERIAL_AUDIO_INVALID` (mã mới). Trước đây mọi file đúng đuôi đều được nhận.
- `PUT {id}/file` của audio xoá lời có mốc thời gian của audio đó.

## 2026-11-09

Đăng ký gia nhập online từ trang chủ (UC-45) và đề cử ca trưởng (UC-46). Cần migration tạo bảng `MemberRegistrations`
và `DirectorNominations` trước khi deploy.

### [Phá vỡ] Bàn giao ca trưởng (bước 11 quy trình)

- `PUT api/users/{id}/role` (đổi khỏi `ChoirDirector`) và `PATCH api/users/{id}/deactivate` trả thêm 409
  `USER_STILL_DIRECTING` (mã mới) khi người đó còn phụ trách ca đoàn hoặc là ca trưởng thay thế ở sự kiện sắp tới.
  Trước đây hai thao tác này luôn thành công. Web hiển thị câu của mã và hướng Admin gán ca trưởng khác trước.
- `NotificationType` thêm `ChoirChanged` (số 13), `referenceType = "Choir"`: báo ca trưởng khi ca viên rời ca đoàn vì
  đổi role, và báo ca trưởng cũ / mới khi `PUT api/choirs/{id}` đổi `directorId`.

### Không phá vỡ — đề cử ca trưởng (UC-46)

- Nhóm endpoint mới `api/director-nominations` (`api.md` mục 4i): `GET eligibility`, `POST`, `GET`, `GET {id}`, và
  `POST {id}/withdraw | approve | reject | cancel | waive-training | appoint`. Trả `DirectorNominationDto` /
  `DirectorNominationDetailDto` (có `stats`).
- Mã mới `DIRECTOR_NOMINATION_NOT_FOUND` (404), `DIRECTOR_NOMINATION_ALREADY_OPEN` (409),
  `DIRECTOR_NOMINATION_INVALID_STATUS` (409), `DIRECTOR_NOMINATION_NOTE_REQUIRED` (400).
- `NotificationType` thêm `DirectorNomination` (số 12), `referenceType = "DirectorNomination"`.
- `SystemSetting` thêm 4 key nhóm "Đề cử ca trưởng" (`Nomination.*`), tự thêm vào DB lúc khởi động.
- Khoá học ca trưởng (`api.md` mục 4i, cuối mục): `GET`/`POST {id}/trainings`, `PUT …/{trainingId}/result`,
  `PUT …/{trainingId}/certificate` (multipart), `DELETE …/{trainingId}`; trả `DirectorTrainingDto`. Mã mới
  `DIRECTOR_TRAINING_NOT_FOUND` (404), `DIRECTOR_TRAINING_IN_PROGRESS` (409), `DIRECTOR_TRAINING_ALREADY_RECORDED` (409),
  `DIRECTOR_TRAINING_CERTIFICATE_REQUIRED` (400), `DIRECTOR_TRAINING_CERTIFICATE_TYPE_NOT_ALLOWED` (400),
  `DIRECTOR_TRAINING_CERTIFICATE_TOO_LARGE` (413). Cần thêm migration bảng `DirectorTrainings`.

### Không phá vỡ — đăng ký gia nhập (UC-45)

- Endpoint mới `POST api/member-registrations` · **anonymous** — form trang chủ `{ fullName, email, phone?, note? }`,
  luôn 204; tối đa 5 lần / 10 phút / IP, quá thì 429 `RATE_LIMIT_EXCEEDED` (mã mới).
- Endpoint mới cho `Admin`: `GET api/member-registrations` (`PagedList<MemberRegistrationDto>`),
  `POST …/{id}/accept` (200 `UserDto`, tạo tài khoản `ChoirMember`), `DELETE …/{id}` (204). Mã mới 404
  `MEMBER_REGISTRATION_NOT_FOUND`.
- `NotificationType` thêm `MemberRegistrationSubmitted` (số 11): Admin nhận khi có đăng ký mới,
  `referenceType = "MemberRegistration"`.

## 2026-11-07

Ca trưởng thay thế cho một sự kiện, chấm điểm tự động bản thu tập hát, và bản nhạc tương tác MusicXML (Web tbd-backlog
B47). Cần migration `AddEventChoirSubstituteDirector` và `AddInteractiveScore` trước khi deploy.

### Không phá vỡ — bản nhạc tương tác (B47)

- `materialType` thêm `InteractiveScore` (số 4): nhận `.musicxml`, `.mxl` ≤ 20 MB qua `POST api/music-materials` và
  `PUT …/{id}/file` như các loại khác (`instrument?` dùng được). Server chuyển score sang MIDI rồi render WAV +
  `notes.json` ngay trong request; lỗi thì không lưu gì. Mã mới 422 `MATERIAL_SCORE_INVALID`, `MATERIAL_SCORE_UNSUPPORTED`.
- Endpoint mới `GET api/music-materials/{id}/score` · mọi role — `InteractiveScoreDto`: `revision`, `scoreFormat`,
  `scoreUrl` + `scoreSha256`, `audioUrl` + `audioSha256`, `durationSeconds`, `instrument`, `status` (luôn `Ready`). Loại
  khác → 409 `MATERIAL_NOT_INTERACTIVE_SCORE` (mã mới). Quy tắc thời gian (không mở rộng nhắc lại, 120 BPM mặc định, nốt
  hoa mỹ không chiếm thời gian) ở `api.md` mục 7.
- `GET …/{id}/playback` (v1, không đổi) cũng trả bản phát của `InteractiveScore`; bài tập gắn tư liệu này dùng được cho
  chấm điểm bản thu.
- `materialCounts` của `SongDto` có thêm khoá `InteractiveScore`; `missingMaterialType` nhận giá trị mới.

### [Phá vỡ] Đổi ca đoàn của sự kiện đã công bố

- `PUT api/liturgical-events/{id}/choirs` nhận thêm `reason?` (≤ 500 ký tự). **Bắt buộc** khi gỡ một ca đoàn khỏi sự kiện
  đã công bố — thiếu → 400 `CHOIR_CHANGE_REASON_REQUIRED` (mã mới). Client đang gỡ ca đoàn mà không gửi lý do sẽ bị chặn.
- Gỡ ca đoàn: ca viên không thuộc ca đoàn nào còn lại bị **xoá lời mời** và **dòng phân công**; phân công đã chốt mở lại
  `Draft`. Đổi ca đoàn phụ trách thì mọi phiên bản `SongList` của sự kiện chuyển sang ca đoàn mới, giữ trạng thái. Gửi
  thông báo `EventChoirChanged` kèm lý do.

### Không phá vỡ

- Endpoint mới `PUT api/liturgical-events/{id}/choirs/{choirId}/substitute-director` · `ParishPriest` — body
  `{ directorId?, reason? }`. Đặt ca trưởng thay thế cho một ca đoàn **chỉ ở sự kiện này** (`reason` bắt buộc khi đặt);
  `directorId: null` gỡ người thay. Người thay có quyền ca trưởng của ca đoàn đó ở sự kiện này và thấy sự kiện trong
  `GET api/liturgical-events`; ca trưởng chính vẫn giữ quyền. Lỗi: 400 `CHOIR_CHANGE_REASON_REQUIRED`,
  `CHOIR_DIRECTOR_INVALID` · 404 `LITURGICAL_EVENT_NOT_FOUND` · 409 `CHOIR_NOT_SERVING_EVENT`, `LITURGICAL_EVENT_CANCELLED`,
  `LITURGICAL_EVENT_ALREADY_PASSED`.
- `LiturgicalEventChoirDto` thêm `substituteDirectorId`, `substituteDirectorFullName`, `reason` (có thể `null`).
- Chấm điểm bản thu theo bản mẫu MIDI của bài tập — chỉ để tham khảo, **không lưu**, không nộp bài. Trả
  `PracticeAudioScoreResponse` `{ score, pitchScore, rhythmScore, coveragePercent, trackName }` (0–100; `score` gồm 70% cao độ
  và 30% nhịp):
  - `POST api/practice-assignments/mine/{id}/score` · `ChoirMember` — multipart `file` (`.mp3`, `.m4a`, `.wav`), chấm trước
    khi nộp.
  - `POST api/practice-submissions/{id}/score` · `ChoirDirector` — chấm bản đã nộp, hỗ trợ ca trưởng nhận xét.
  - Mã mới 422 `PRACTICE_AUDIO_UNREADABLE` (không đọc được / quá ít tiếng hát), 422 `PRACTICE_REFERENCE_MISSING` (bài tập
    chưa có bản mẫu MIDI).

## 2026-11-06

### Không phá vỡ

- Endpoint mới `POST api/event-participations/confirm-all?eventId=` · `ChoirDirector` (ca đoàn phụ trách) — xác nhận hộ mọi
  lời mời `Invited` của ca viên `Active` (giữ `Declined` / `Unsure`), phục vụ demo; không gửi thông báo. Trả
  `EventParticipationSummaryDto`. Lỗi: 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND` · 409
  `LITURGICAL_EVENT_CANCELLED`.

## 2026-11-05

### Không phá vỡ

- Setting mới `Rehearsal.AttendanceStartCheckEnabled` (Bool, mặc định `true` = như cũ). Tắt thì điểm danh được bất cứ lúc
  nào, không còn `REHEARSAL_NOT_STARTED` (dùng khi demo). Tự thêm khi khởi động, không cần migration.

### [Phá vỡ] Sự kiện bắt buộc có ca đoàn

Web cha xứ phải sửa theo: form tạo sự kiện, thẻ gán ca đoàn, bản dịch mã mới. Mobile không ảnh hưởng.

- `POST api/liturgical-events` thêm **bắt buộc** `choirIds` (ít nhất một) và `leadChoirId` (nằm trong `choirIds`).
  Thiếu → 400 `VALIDATION_FAILED` (`LITURGICAL_EVENT_CHOIR_REQUIRED`, `CHOIR_LEAD_INVALID`). Ca đoàn sai → 404
  `CHOIR_NOT_FOUND`, đã tắt → 409 `CHOIR_INACTIVE`.
- `PATCH api/liturgical-events/{id}/publish`: sự kiện chưa có ca đoàn phụ trách → 400 `LITURGICAL_EVENT_CHOIR_REQUIRED`.
  `POST .../generation` vẫn sinh nháp không ca đoàn; gán qua `PUT .../choirs` rồi mới công bố.
- `PUT api/liturgical-events/{id}/choirs` không nhận `choirIds: []` nữa → 400 `VALIDATION_FAILED`
  (`LITURGICAL_EVENT_CHOIR_REQUIRED`); `leadChoirId` luôn bắt buộc.
- Sự kiện cũ chưa gán ca đoàn: ca trưởng vẫn xem được, nhưng tạo / sửa / nộp danh sách bài, mời, phân công trả 403
  `CHOIR_NOT_LEAD` cho tới khi cha xứ gán ca đoàn.

## 2026-11-03

Minh chứng kỹ năng và role trên hồ sơ ca viên. Cần migration `AddMemberSkillEvidence` trước khi deploy.

### [Phá vỡ] Hồ sơ ca viên đã đổi role

- Đổi tài khoản từ `ChoirMember` sang role khác: hồ sơ được **giữ lại** nhưng chuyển `Inactive` và rời mọi ca đoàn. Đổi
  lại về `ChoirMember` thì `Inactive` về `Active`; ca trưởng phải thêm lại vào ca đoàn.
- `PUT api/member-profiles/{id}` với hồ sơ đó → 409 `MEMBER_NOT_ACTIVE` (trước đây sửa được).

### Không phá vỡ

- `MemberProfileSummaryDto` (`GET api/member-profiles`) thêm `roleName` — role **hiện tại** của tài khoản (Web tbd-backlog
  B46). `MemberProfileDetailDto` vẫn có `roleName` như cũ.
- Endpoint mới `PUT api/member-skills/{id}/evidence` · `ChoirMember` — multipart `file`, minh chứng cho kỹ năng đang
  `Pending`: ghi âm `.mp3`, `.m4a`, `.wav` ≤ 20 MB hoặc clip `.mp4`, `.mov` ≤ 100 MB; gửi lại thì thay file cũ. Trả
  `MemberSkillDto`. Mã mới 400 `MEMBER_SKILL_EVIDENCE_REQUIRED`, `MEMBER_SKILL_EVIDENCE_TYPE_NOT_ALLOWED`, 413
  `MEMBER_SKILL_EVIDENCE_TOO_LARGE`; ngoài ra 404 `MEMBER_NOT_FOUND`, `MEMBER_SKILL_NOT_FOUND` · 409 `MEMBER_NOT_ACTIVE`,
  `MEMBER_SKILL_ALREADY_REVIEWED` · 502 `EXTERNAL_STORAGE_FAILED`.
- `MemberSkillDto` thêm `evidence` (`null` khi chưa gửi): `{ type: Voice | Clip, fileName, fileSizeBytes, uploadedAt, url }`
  — `url` là link ký có hạn, hết hạn thì gọi lại API. Ca trưởng thấy ở `GET api/member-skills/pending`; duyệt không bắt
  buộc có minh chứng.

## 2026-11-01

Gỡ bớt việc FE tự tính (Web backlog B39, B41, B43, B44, B45). Cần migration `AddLiturgicalDayColor` (B45) và `AddRehearsalRecording` (B43).

### [Phá vỡ] Thiếu người

- `GET api/service-rosters/shortages` trả `[]` khi chưa có gì để phân công (chưa có danh sách bài đã duyệt, không có yêu
  cầu cả buổi lễ), thay cho 409 `ROSTER_SONG_LIST_NOT_APPROVED`. `suggestions`, `finalize`, thêm tay vẫn trả 409 như cũ.
- `EventPreparationStatusDto.rosterShortages` không còn `null`: trường hợp trên trả `[]`.

### Không phá vỡ

- `GET api/liturgical-events/{id}/preparation-status` mở thêm cho `ChoirDirector`, trong phạm vi ca đoàn mình phục vụ
  (ngoài phạm vi → 404 `LITURGICAL_EVENT_NOT_FOUND`).
- Thêm `GET api/song-lists/event/{eventId}/versions` (`ChoirDirector`, `ParishPriest`): mọi phiên bản kèm `items` và
  `reviews`, mới nhất trước; cha xứ không thấy `Draft`.
- `SongListDto.items` sắp theo `displayOrder`, `reviews` theo `reviewedAt` tăng dần.
- **B45** `LiturgicalDayDto` thêm `color` (màu phụng vụ: `white` | `red` | `green` | `purple` | `rose` | `black`). Cần
  migration `AddLiturgicalDayColor`; ngày đã cache có `color = null` tới khi cha xứ đồng bộ lại năm đó.
- Ảnh đại diện: `PUT api/auth/me/avatar` (multipart `file`, `.jpg`, `.jpeg`, `.png`, `.webp` ≤ 2 MB) và
  `DELETE api/auth/me/avatar` · mọi role, chỉ ảnh của chính mình. Ảnh cũ bị xoá khỏi kho. `UserDto`, `MemberProfileDto`
  thêm `avatarUrl` — URL **công khai**, có thể `null`.
- **B43** Bản thu buổi tập: `GET` / `POST api/rehearsals/{id}/recordings`, `DELETE …/recordings/{recordingId}`. Ca trưởng
  ghi và xoá; ca viên của ca đoàn buổi tập nghe được. Nhận `.webm`, `.ogg`, `.m4a`, `.mp3`, `.wav` ≤ 20 MB. Mã mới
  `REHEARSAL_RECORDING_NOT_FOUND`, `_REQUIRED`, `_TYPE_NOT_ALLOWED`, `_TOO_LARGE`. Cần migration `AddRehearsalRecording`.
- **B44** `LiturgicalEventListItemDto` (`GET api/liturgical-events`) thêm `songListDueDate` = ngày lễ − setting mới
  `SongList.LeadDays` (mặc định 7, Admin sửa ở trang cấu hình). Chỉ để nhắc, gửi trễ vẫn được.

## 2026-10-29 / 30

Tư liệu bài hát: lời dạng `.docx` hoặc gõ thẳng, phát MIDI, AI đọc lời từ bản nhạc, tìm bài theo lời. Cần migration
`AddMusicMaterialLyricsText`, `AddMusicMaterialPlayback` trước khi deploy, và key `Gemini__*` (đã có từ gợi ý phân công).

### [Phá vỡ] Tư liệu bài hát

- `Lyrics` chỉ nhận file `.docx` **hoặc** lời gõ thẳng `lyricsText` (≤ 20 000 ký tự), đúng một trong hai — sai →
  400 `MATERIAL_LYRICS_SOURCE_INVALID` (mã mới). **Bỏ** `.pdf`, `.png`, `.jpg` cho `Lyrics` (trước đây nhận): gửi các đuôi
  này → 400 `MATERIAL_FILE_TYPE_NOT_ALLOWED`. Lời cũ dạng `.pdf` / `.png` / `.jpg` vẫn xem được; thay file thì phải `.docx`.
- `MusicMaterialDto.fileName` và `fileUrl` **có thể `null`** (lời gõ thẳng, không có file). Client đang coi hai trường
  này luôn có giá trị phải đổi. Thêm trường `lyricsText` (null với tư liệu có file).
- `PUT api/music-materials/{id}` nhận thêm `lyricsText`: **bắt buộc** với lời gõ thẳng, **phải bỏ trống** với tư liệu có
  file (sai → 400 `MATERIAL_LYRICS_SOURCE_INVALID`). Client không gửi trường này vẫn chạy với tư liệu có file.
- `PUT api/music-materials/{id}/file` với lời gõ thẳng → 400 `MATERIAL_LYRICS_SOURCE_INVALID` (không có file để thay).

### Không phá vỡ

- `SampleAudio` và `RehearsalMaterial` nhận thêm `.mid`, `.midi`. Upload / thay file MIDI nhận `instrument?`
  (`Piano` | `Organ` | `Guitar` | `StringEnsemble`, mặc định `Piano`); server render WAV + `notes.json` ngay trong request.
  File MIDI không render được → 422 `MIDI_INVALID_FILE`, `MIDI_FORMAT_UNSUPPORTED`, `MIDI_NO_PLAYABLE_NOTES`,
  `MIDI_LIMIT_EXCEEDED` (mã mới; quá 10 phút hoặc 100 000 nốt). Trình duyệt không phát MIDI trực tiếp.
- Endpoint mới:
  - `GET api/music-materials/{id}/playback` — `PlaybackDto` (`audioUrl`, `notesUrl` có hạn, `durationSeconds`,
    `instrument`). Không phải MIDI, hoặc MIDI tải lên trước tính năng này → 409 `MATERIAL_PLAYBACK_UNAVAILABLE` (mã mới).
  - `POST api/music-materials/{id}/extract-lyrics` · chỉ `ChoirDirector` — AI đọc lời từ bản nhạc, trả `{ lyricsText }`
    bản nháp, **không lưu**; lưu bằng upload `Lyrics` với `lyricsText`. Không phải `SheetMusic` → 400
    `MATERIAL_NOT_SHEET_MUSIC` (mã mới); AI lỗi → 502 `EXTERNAL_AI_FAILED`.
- `GET api/songs?keyword=` tìm thêm trong lời gõ thẳng (`lyricsText`) của tư liệu `Lyrics` đang hoạt động, không phân
  biệt dấu.

## 2026-10-27

### Phân công phục vụ: chặn trùng giờ (Web tbd-backlog B40)

- Mã lỗi mới 409 `ROSTER_MEMBER_TIME_CONFLICT` cho `POST api/service-rosters/assignments` và
  `POST …/assignments/{id}/replacement`: ca viên đã có dòng `Active` ở một lễ khác chưa huỷ (mọi ca đoàn) cùng ngày, bắt
  đầu cách nhau ít hơn `Roster.EventOverlapMinutes` phút.
- `POST api/service-rosters/suggestions` bỏ qua những ca viên đó; chỗ thiếu hiện trong `shortages` như thường.
- Setting mới `Roster.EventOverlapMinutes` (Int 0–720, mặc định 120, nhóm "Phân công"), tự thêm khi khởi động. Không cần
  migration.

## 2026-10-26

Phân loại bài hát chi tiết hơn (theo dieuca.net) và phân công 3 tầng: đồng ca không phân công, vai **cả buổi lễ**
(organ, bắt nhịp), vai **theo bài** (xướng đáp ca, solo). Cần migration trước khi deploy.

### [Phá vỡ] Phân công phục vụ

- `RosterShortageDto.songListItemId` và `songTitle` **có thể `null`**: dòng thiếu người cho cả buổi lễ. Web đang hiển thị
  `"{songTitle}: thiếu …"` phải đổi cho dòng này (ví dụ "Cả buổi lễ: thiếu 1 Organ").
- `RosterShortageDto.requiredCount` của dòng **theo bài** giờ là **phần thêm** ngoài người cả buổi lễ: bài cần Guitar ×2, cả
  lễ có Guitar ×1 → dòng thiếu của bài có `requiredCount = 1`. Hiển thị "cần **thêm** 1 guitar". Bài cần ≤ số của cả lễ không
  còn sinh dòng thiếu nào.
- `RosterAssignmentDto.songListItemId = null` = dòng cả buổi lễ. `MyAssignmentDto` cũng vậy.
- `GET api/liturgical-events/{id}/preparation-status`: `rosterShortages` **khác `null`** khi sự kiện có yêu cầu cả buổi lễ,
  kể cả khi chưa có danh sách bài được duyệt.
- `POST api/service-rosters/suggestions`, `GET …/shortages`: không còn trả `ROSTER_SONG_LIST_NOT_APPROVED` khi sự kiện có yêu
  cầu cả buổi lễ — chạy trên phần cả buổi lễ.
- `ROSTER_ASSIGNMENT_DUPLICATE` thêm trường hợp: một người giữ kỹ năng cho cả buổi lễ không được gán thêm cùng kỹ năng ở một
  bài, và ngược lại (thêm và thay người).
- `PUT api/song-list-items/{id}/personnel-requirements`: bỏ một kỹ năng đang có người được gán → 409
  `PERSONNEL_REQUIREMENT_IN_USE` (trước đây gỡ được, để lại dòng phân công mồ côi).

### [Phá vỡ] Chủ đề bài hát

- `POST` / `PUT api/lookups/song-themes` nhận thêm `parentId?`; `GET` trả thêm `parentId`. Chủ đề thành cây nhiều cấp.
- Tắt chủ đề còn con đang hoạt động → 409 `SONG_THEME_HAS_ACTIVE_CHILDREN` (mã mới). Bật / tạo chủ đề dưới cha đã tắt → 400
  `SONG_THEME_PARENT_INVALID` (mã mới).
- `GET api/songs?songThemeId=` giờ trả cả bài của các chủ đề con cháu.

### Không phá vỡ

- Endpoint mới:
  - `GET` / `PUT api/liturgical-events/{id}/personnel-requirements` — vai cả buổi lễ.
  - `POST api/service-rosters/{id}/whole-service-finalization` — chốt sớm vai cả buổi lễ; sau đó dòng cả buổi lễ chỉ còn
    **thay người** được, `notifications` gửi được ngay cho các dòng đó. `ServiceRosterDto` thêm `wholeServiceFinalizedAt`.
  - `PUT api/service-rosters/assignments/{id}/note` — ghi chú vai. `RosterAssignmentDto`, `MyAssignmentDto` thêm `note`;
    thêm / thay người nhận `note?`.
  - `GET api/liturgical-events/{id}/song-suggestions?slotId=` — gợi ý bài cho sự kiện.
  - `GET` / `PUT api/lookups/liturgical-slots/{id}/requirements` — người vị trí luôn cần (Admin ghi).
- `POST api/service-rosters/assignments`: `songListItemId` thành tuỳ chọn (bỏ trống = dòng cả buổi lễ).
- Phân loại bài: `liturgicalSlotIds` / `liturgicalSlots` (vị trí trong lễ); `vocalRequirements` / `instrumentRequirements` thêm
  `requiredCount` (mặc định 1). `GET api/songs` thêm lọc `liturgicalSlotId`.
- `POST api/liturgical-events`: bỏ trống `liturgicalSeasonId` → server điền mùa đang hoạt động **chứa ngày lễ**. Lễ Vọng tối
  24/12 vì vậy được điền Mùa Vọng; cha xứ tự chọn Giáng Sinh nếu muốn (chủ ý, không tự đoán theo giờ).
- Yêu cầu nhân sự của bài được **điền sẵn** khi tạo / sửa danh sách: giữ yêu cầu của cùng bài cùng vị trí ở bản trước; bài mới
  lấy từ thư viện (bỏ bè S/A/T/B) + mặc định của vị trí. Sửa danh sách không còn làm mất yêu cầu đã khai.
- Dữ liệu mẫu (loại lễ, nghi thức, vị trí trong lễ, cây chủ đề, mặc định Đáp ca / Tung hô Tin Mừng) chỉ nạp vào **bảng đang
  trống**. Trên Azure các bảng đã có dữ liệu thì Admin tự cấu hình; mặc định Đáp ca chỉ nạp khi đã có kỹ năng nhóm Đáp ca.
- Mở quyền (Web tbd-backlog B37, B38): `GET api/choirs` mở thêm cho `ParishPriest` (nhận mọi ca đoàn, như Admin);
  `GET api/member-profiles` mở thêm cho `Admin`.
- `GET api/schedule/rehearsals` nhận thêm `fromDate?`, `toDate?` (ngày giáo xứ, tính cả hai đầu, ≤ 93 ngày): gửi đủ cả hai
  thì trả buổi tập bắt đầu trong khoảng, cả quá khứ lẫn tương lai; không gửi thì như cũ (sắp tới). Gửi một trong hai hoặc
  `fromDate > toDate` → 400 `REPORT_DATE_RANGE_INVALID`, quá 93 ngày → 400 `REPORT_DATE_RANGE_TOO_LARGE`.

## 2026-10-25

### [Phá vỡ] Nhiều ca đoàn — phạm vi ca trưởng (D8)

- Mỗi ca đoàn có **một** ca trưởng (`Choir.directorId`), do **Admin** gán. Chỉ Admin `POST` / `PUT api/choirs`.
- Ca trưởng chỉ thấy và quản lý ca đoàn của mình; ca đoàn của người khác trả **404** `CHOIR_NOT_FOUND`.
- Gán ca đoàn phục vụ và **ca đoàn phụ trách** (`isLead`) cho sự kiện: chỉ **ParishPriest**
  (`PUT api/liturgical-events/{id}/choirs`). Ca trưởng chỉ `GET`.
- Sự kiện nhiều ca đoàn: chỉ ca trưởng của ca đoàn phụ trách lập danh sách bài, mời tham gia, phân công. Ca đoàn phục vụ
  khác chỉ xem; sửa trả **403** `CHOIR_NOT_LEAD`. Có ca đoàn mà chưa chọn phụ trách: ca trưởng mọi ca đoàn phục vụ đều
  sửa được. Bảng đầy đủ ở `api.md` mục 4f.
- Bản ghi chưa gắn ca đoàn (sự kiện không có ca đoàn nào) thì ca trưởng nào cũng quản lý được như trước.

### Không phá vỡ

- Thông báo công bố / huỷ sự kiện: sự kiện đã gắn ca đoàn chỉ gửi cho ca viên và ca trưởng của các ca đoàn phục vụ;
  sự kiện chưa gắn ca đoàn vẫn gửi cả giáo xứ.
- `DELETE api/songs/{id}`: bài đã nằm trong danh sách bài hát, buổi tập hoặc bài luyện tập trả **409** `SONG_IN_USE`.
- `GET api/system-settings/public` gọi được **không cần đăng nhập** (chỉ trả `Parish.Name`, `Choir.Name`,
  `Contact.Email`). Thêm key `Contact.Email`. `SystemSettingDto` thêm `displayName`, `group`, `order`; danh sách sắp
  theo `order` thay vì `key`.
- `GET api/reports/columns/{type}`: cột và tiêu đề tiếng Việt của báo cáo, trùng với file xuất.
- `GET api/reports/activity-trend`: điểm danh và luyện tập theo tuần / tháng / năm cho Admin và ca trưởng.
- `GET api/songs`: tìm không phân biệt dấu (cả tìm ca viên, tìm tài liệu); thêm bộ lọc `isClassified`,
  `missingMaterialType`; `SongDto` thêm `materialCounts`, `isClassified`, `timesUsed`, `lastUsedDate`.
- `PUT api/music-materials/{id}/file`: thay file tài liệu, giữ tiến độ học (tuỳ chọn `resetProgress`).
- Bài luyện tập nhiều tài liệu: request thêm `materialIds`; `PracticeAssignmentDto` thêm `materialIds`,
  `PracticeAssignmentDetailDto` thêm `materials`. `materialId` vẫn còn (= tài liệu đầu tiên).
- `LiturgicalDayDto` thêm `celebrationNameVi`, `rankVi`, `seasonNameVi`; bản dịch do Admin quản lý ở
  `api/liturgical-name-translations`.

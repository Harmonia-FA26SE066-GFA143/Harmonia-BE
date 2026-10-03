# Harmonia API

Tài liệu cho FE (web) và mobile. Chỉ liệt kê endpoint **đã có trong code**; thêm controller
mới thì cập nhật file này trong cùng PR.

- Base URL dev: `http://localhost:5259` · `https://localhost:7112`
- Swagger (dev): `/swagger`
- Định dạng: JSON, tên field `camelCase`. Enum truyền bằng **tên** (`"Android"`, không phải `0`).
- Thời gian: `DateTime` là UTC ISO-8601, `DateOnly` dạng `yyyy-MM-dd`.

## 1. Quy ước chung

### Xác thực

Header `Authorization: Bearer <accessToken>` cho mọi endpoint trừ nhóm `[AllowAnonymous]`.
Access token sống ngắn (15–60 phút, xem `accessTokenExpiresAt`). Hết hạn → gọi
`POST /api/auth/refresh`. Refresh token xoay vòng: mỗi lần refresh token cũ bị thu hồi,
**phải lưu token mới**.

Bốn role: `Admin`, `ParishPriest`, `ChoirDirector`, `ChoirMember`.

### Mã trạng thái thành công

| Service trả | HTTP |
|---|---|
| có dữ liệu | `200 OK` + body |
| không có dữ liệu | `204 No Content` |

### Lỗi

Mọi lỗi cùng một dạng:

```json
{ "code": "AUTH_INVALID_CREDENTIALS", "message": "..." }
```

Lỗi validate (`400`) có thêm `errors` — key là tên field, value là mảng mã lỗi:

```json
{
  "code": "VALIDATION_FAILED",
  "message": "...",
  "errors": {
    "email": ["AUTH_EMAIL_INVALID_FORMAT"],
    "newPassword": ["AUTH_PASSWORD_TOO_WEAK"]
  }
}
```

FE hiển thị theo `code`, **không** hiển thị `message`. Bản dịch và HTTP status của từng mã:
[error-codes.md](error-codes.md).

| Trường hợp | HTTP | `code` |
|---|---|---|
| Thiếu / sai access token | 401 | `AUTH_TOKEN_INVALID` |
| Access token hết hạn | 401 | `AUTH_TOKEN_EXPIRED` → gọi refresh |
| Sai role | 403 | *(body rỗng)* |
| Body JSON hỏng / sai kiểu | 400 | `VALIDATION_FAILED`, `errors.request` hoặc `errors.<field>` |
| Lỗi không lường trước | 500 | `INTERNAL_ERROR` |

### Phân trang

Endpoint danh sách nhận query `pageNumber` (mặc định 1) và `pageSize` (mặc định 20, tối đa 100;
giá trị ngoài khoảng tự kẹp lại, không báo lỗi). Trả về:

```json
{
  "items": [],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

### Mật khẩu mạnh

Ít nhất 8 ký tự, có ít nhất một chữ cái và một chữ số. Áp cho `change-password`,
`reset-password`. (`POST /api/users` hiện chỉ kiểm tối thiểu 8 ký tự.)

---

## 2. Auth — `api/auth`

Không có đăng ký công khai; tài khoản do Admin tạo.

### `LoginResponse` (dùng chung cho login / google / refresh)

```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-10-01T08:30:00Z",
  "refreshToken": "q8x...",
  "user": { "id": "guid", "email": "a@b.com", "fullName": "Nguyễn Văn A", "roleName": "ChoirMember" }
}
```

### `POST /api/auth/login` — anonymous

```json
{ "email": "a@b.com", "password": "Secret123", "deviceId": "optional", "platform": "Android" }
```

`platform`: `Android` | `iOS` | `Web` | `null`.

| HTTP | Kết quả |
|---|---|
| 200 | `LoginResponse` |
| 400 | `AUTH_EMAIL_REQUIRED`, `AUTH_EMAIL_INVALID_FORMAT`, `AUTH_PASSWORD_REQUIRED` |
| 401 | `AUTH_INVALID_CREDENTIALS` — sai email hoặc mật khẩu (không phân biệt) |
| 403 | `AUTH_ACCOUNT_INACTIVE` |

### `POST /api/auth/google` — anonymous

```json
{ "idToken": "<Google ID token>", "deviceId": "optional", "platform": "Web" }
```

Chỉ đăng nhập được nếu email Google đã có tài khoản — không tự tạo tài khoản.

| HTTP | Kết quả |
|---|---|
| 200 | `LoginResponse` |
| 400 | `AUTH_GOOGLE_TOKEN_REQUIRED` |
| 401 | `AUTH_GOOGLE_TOKEN_INVALID`, `AUTH_INVALID_CREDENTIALS` (email chưa có tài khoản) |
| 403 | `AUTH_ACCOUNT_INACTIVE` |

### `POST /api/auth/refresh` — anonymous

```json
{ "refreshToken": "q8x..." }
```

| HTTP | Kết quả |
|---|---|
| 200 | `LoginResponse` mới (token cũ đã bị thu hồi) |
| 400 | `AUTH_REFRESH_TOKEN_REQUIRED` |
| 401 | `AUTH_REFRESH_TOKEN_NOT_FOUND`, `AUTH_REFRESH_TOKEN_REVOKED`, `AUTH_REFRESH_TOKEN_EXPIRED` → về màn đăng nhập |
| 403 | `AUTH_ACCOUNT_INACTIVE` → về màn đăng nhập |

Admin vô hiệu hoá tài khoản hoặc đổi role → mọi refresh token của user đó bị thu hồi. Lần refresh
kế tiếp nhận `401 AUTH_REFRESH_TOKEN_REVOKED` (hoặc `403 AUTH_ACCOUNT_INACTIVE`), user phải đăng
nhập lại. Access token đang cầm vẫn chạy tới khi hết hạn (tối đa `Jwt__ExpiryMinutes`).

### `POST /api/auth/logout` — đã đăng nhập

```json
{ "refreshToken": "q8x..." }
```

`204` · `400 AUTH_REFRESH_TOKEN_REQUIRED` · `401 AUTH_REFRESH_TOKEN_NOT_FOUND`

### `POST /api/auth/logout-all` — đã đăng nhập

Không body. Thu hồi mọi refresh token của user (đăng xuất mọi thiết bị). `204`.

### `POST /api/auth/change-password` — đã đăng nhập

```json
{ "currentPassword": "Old12345", "newPassword": "New12345" }
```

Thành công thì mọi thiết bị bị đăng xuất — client phải đăng nhập lại.

| HTTP | Kết quả |
|---|---|
| 204 | Đổi xong |
| 400 | `AUTH_PASSWORD_REQUIRED`, `AUTH_PASSWORD_TOO_WEAK`, `AUTH_CURRENT_PASSWORD_INVALID` |
| 404 | `USER_NOT_FOUND` |

### `POST /api/auth/forgot-password` — anonymous

```json
{ "email": "a@b.com", "platform": "iOS" }
```

**Luôn trả `204`** dù email có tồn tại hay không. Email chứa link
`<WebUrl|MobileUrl>?token=...` (Android/iOS → link mobile, còn lại → link web),
hết hạn sau 1 giờ, dùng một lần; chỉ link mới nhất còn hiệu lực.

`400`: `AUTH_EMAIL_REQUIRED`, `AUTH_EMAIL_INVALID_FORMAT`, `VALIDATION_FAILED` (platform sai).

### `POST /api/auth/reset-password` — anonymous

```json
{ "token": "<token từ link email>", "newPassword": "New12345" }
```

Thành công thì mọi thiết bị bị đăng xuất.

| HTTP | Kết quả |
|---|---|
| 204 | Đặt lại xong |
| 400 | `AUTH_RESET_TOKEN_INVALID`, `AUTH_RESET_TOKEN_EXPIRED`, `AUTH_RESET_TOKEN_USED`, `AUTH_PASSWORD_REQUIRED`, `AUTH_PASSWORD_TOO_WEAK` |

---

## 3. Users — `api/users` · role `Admin`

### `UserDto`

```json
{ "id": "guid", "email": "a@b.com", "fullName": "Nguyễn Văn A", "roleName": "ChoirDirector", "isActive": true }
```

`fullName` có ở **mọi role** (lưu trên `User`). Tài khoản tạo trước 2026-10-03 có thể là `""`
cho tới khi Admin cập nhật.

| Method | Route | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/users` | query `keyword`, `roleName`, `isActive`, `pageNumber`, `pageSize` | 200 `PagedList<UserDto>` | — |
| GET | `/api/users/{id}` | — | 200 `UserDto` | 404 `USER_NOT_FOUND` |
| POST | `/api/users` | `{ "email", "fullName", "password", "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `ROLE_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PUT | `/api/users/{id}` | `{ "email", "fullName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PATCH | `/api/users/{id}/activate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_ALREADY_ACTIVE` |
| PATCH | `/api/users/{id}/deactivate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_ALREADY_INACTIVE`, `USER_LAST_ADMIN` |
| PUT | `/api/users/{id}/role` | `{ "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND`, `ROLE_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_LAST_ADMIN` |

- `GET /api/users`: `keyword` khớp một phần email; các bộ lọc kết hợp AND; sắp theo email.
- `fullName` bắt buộc, tối đa 100 ký tự, khoảng trắng đầu/cuối bị cắt.
- `roleName` phải là một trong 4 role. Lỗi validate của nhóm này hiện chỉ trả
  `VALIDATION_FAILED` trong `errors`, chưa có mã riêng theo field.
- Tạo tài khoản role `ChoirMember` (hoặc đổi role sang `ChoirMember`) → hồ sơ ca viên được tạo
  tự động (`status = Active`, `joinedDate` = hôm nay). Đổi role khỏi `ChoirMember` thì hồ sơ vẫn giữ.
- `deactivate` và đổi role thu hồi mọi phiên đăng nhập của user đó (xem refresh ở mục 2).

---

## 4. Member profiles — `api/member-profiles` · của tôi: `ChoirMember` · quản lý: `ChoirDirector`

### `MemberProfileDto`

```json
{
  "id": "guid",
  "fullName": "Nguyễn Văn A",
  "email": "a@b.com",
  "phone": "0900000000",
  "dateOfBirth": "2000-01-31",
  "joinedDate": "2024-09-01",
  "status": "Active",
  "avatarUrl": null
}
```

`status`: `Active` | `Inactive` | `Left`. `phone`, `dateOfBirth`, `avatarUrl` có thể `null`.
`fullName`, `email`, `avatarUrl` lấy từ tài khoản (`User`).

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/member-profiles/me` | `ChoirMember` | — | 200 `MemberProfileDto` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/member-profiles/me` | `ChoirMember` | `{ "fullName", "phone", "dateOfBirth" }` | 200 `MemberProfileDto` | 400 `VALIDATION_FAILED` · 404 `MEMBER_NOT_FOUND` |
| GET | `/api/member-profiles` | `ChoirDirector` | query `keyword`, `status`, `pageNumber`, `pageSize` | 200 `PagedList<MemberProfileDto>` | — |
| GET | `/api/member-profiles/{id}` | `ChoirDirector` | — | 200 `MemberProfileDto` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/member-profiles/{id}` | `ChoirDirector` | `{ "phone", "dateOfBirth", "joinedDate", "status" }` | 200 `MemberProfileDto` | 400 `VALIDATION_FAILED`, `MEMBER_JOINED_DATE_IN_FUTURE` · 404 `MEMBER_NOT_FOUND` |

- `PUT` thay **toàn bộ** các field trong body: field bỏ trống / `null` sẽ bị xoá giá trị
  (`phone`, `dateOfBirth`). Gửi lại giá trị cũ nếu không muốn đổi.
- `PUT .../me`: `fullName` bắt buộc (≤ 100 ký tự), đổi tên hiển thị của chính tài khoản đó.
  `phone` ≤ 20 ký tự. `dateOfBirth` không được ở tương lai.
- `PUT .../{id}`: `joinedDate` bắt buộc, không ở tương lai. Ca trưởng **không** sửa tên ca viên
  (tên do ca viên tự sửa, hoặc Admin sửa qua `PUT /api/users/{id}`).
- `GET /api/member-profiles`: `keyword` khớp một phần tên hoặc email; sắp theo tên.
- Role khác gọi vào → `403` body rỗng.

---

## 5. Notifications — `api/notifications` · mọi role đã đăng nhập

Chỉ thao tác trên thông báo **của chính người gọi**.

### `NotificationDto`

```json
{
  "id": "guid",
  "type": "SongListDecision",
  "title": "...",
  "content": "...",
  "referenceType": "SongList",
  "referenceId": "guid",
  "createdAt": "2026-10-01T08:00:00Z",
  "isRead": false,
  "readAt": null
}
```

`type`: `EventPublished` | `SongListDecision` | `ParticipationRequest` | `AssignmentNotice` |
`PracticeFeedback` | `DirectorNote`. `referenceType` + `referenceId` (nullable) cho biết bấm vào
thì mở màn nào.

| Method | Route | Thành công | Lỗi |
|---|---|---|---|
| GET | `/api/notifications?pageNumber=1&pageSize=20` | 200 `PagedList<NotificationDto>` | — |
| GET | `/api/notifications/unread-count` | 200, body là số nguyên (`3`) | — |
| PUT | `/api/notifications/{id}/read` | 204 | 404 `NOTIFICATION_NOT_FOUND` |

---

## 6. Songs — `api/songs` · xem: mọi role · sửa: `ChoirDirector`

Thư viện bài hát và phân loại bài hát (UC-21 / FE-27, FE-29).

### `SongDto`

```json
{
  "id": "guid",
  "title": "Kinh Hòa Bình",
  "composer": "Kim Long",
  "lyricist": null,
  "musicalKey": "G",
  "tempo": "Andante",
  "notes": null
}
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/songs?keyword=&liturgicalSeasonId=&massTypeId=&ceremonyTypeId=&songThemeId=&skillId=&pageNumber=1&pageSize=20` | — | 200 `PagedList<SongDto>` | — |
| GET | `/api/songs/{id}` | — | 200 `SongDto` | 404 `SONG_NOT_FOUND` |
| POST | `/api/songs` | `{ "title", "composer", "lyricist", "musicalKey", "tempo", "notes" }` | 200 `SongDto` | 400 `VALIDATION_FAILED` · 409 `SONG_TITLE_DUPLICATE` |
| PUT | `/api/songs/{id}` | như POST | 200 `SongDto` | 400 `VALIDATION_FAILED` · 404 `SONG_NOT_FOUND` · 409 `SONG_TITLE_DUPLICATE` |
| DELETE | `/api/songs/{id}` | — | 204 | 404 `SONG_NOT_FOUND` |
| GET | `/api/songs/{id}/classification` | — | 200 `SongClassificationDto` | 404 `SONG_NOT_FOUND` |
| PUT | `/api/songs/{id}/classification` | `UpdateSongClassificationRequest` | 200 `SongClassificationDto` | 400 `VALIDATION_FAILED`, `SONG_CLASSIFICATION_DUPLICATE`, `SONG_SKILL_REQUIREMENT_DUPLICATE`, `SONG_CLASSIFICATION_TARGET_INVALID`, `SONG_SKILL_REQUIREMENT_CATEGORY_INVALID` · 404 `SONG_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE` |

- `keyword` tìm trong `title`, `composer`, `lyricist`; kết quả sắp theo `title`.
- Chỉ `title` bắt buộc (≤ 200). `composer`, `lyricist` ≤ 150 · `musicalKey` ≤ 10 · `tempo` ≤ 50 ·
  `notes` ≤ 1000. Chuỗi được trim, chuỗi rỗng lưu thành `null`.
- `SONG_TITLE_DUPLICATE`: đã có bài cùng `title` **và** cùng `composer` (cùng tên khác nhạc sĩ vẫn được).
- `DELETE` là xoá mềm, không khôi phục được: bài biến khỏi danh sách và mọi route theo `id`
  trả `SONG_NOT_FOUND`; danh sách bài hát cũ vẫn giữ tham chiếu. Tạo lại bài cùng tên được.
- Bộ lọc phân loại của `GET /api/songs` đều tuỳ chọn và kết hợp theo AND. `skillId` khớp bài có
  yêu cầu bè **hoặc** nhạc cụ là skill đó.

### Phân loại bài hát — `SongClassificationDto` / `UpdateSongClassificationRequest`

```json
{
  "songId": "guid",
  "liturgicalSeasons": [{ "id": "guid", "name": "Mùa Vọng" }],
  "massTypes": [],
  "ceremonyTypes": [],
  "songThemes": [{ "id": "guid", "name": "Đức Mẹ" }],
  "vocalRequirements": [{ "skillId": "guid", "skillName": "Soprano", "isMandatory": true }],
  "instrumentRequirements": [{ "skillId": "guid", "skillName": "Organ", "isMandatory": false }]
}
```

```json
{
  "liturgicalSeasonIds": ["guid"],
  "massTypeIds": [],
  "ceremonyTypeIds": [],
  "songThemeIds": ["guid"],
  "vocalRequirements": [{ "skillId": "guid", "isMandatory": true }],
  "instrumentRequirements": [{ "skillId": "guid", "isMandatory": false }]
}
```

- `PUT` **thay cả bộ**: mục nào không gửi lên thì bị gỡ khỏi bài. Gửi `[]` để gỡ hết một nhóm.
- Id trùng trong cùng một danh sách → 400, `errors.<field>` chứa `SONG_CLASSIFICATION_DUPLICATE` /
  `SONG_SKILL_REQUIREMENT_DUPLICATE` (bắt ở validator, chưa tới DB).
- Mùa / loại lễ / nghi thức / chủ đề **thêm mới** phải tồn tại và đang hoạt động, nếu không → 400
  `SONG_CLASSIFICATION_TARGET_INVALID`. Skill **thêm mới** phải đang hoạt động (409 `SKILL_INACTIVE`).
  Mục bài đã có từ trước vẫn giữ được dù sau đó bị vô hiệu hoá, và `GET` vẫn hiển thị.
- `instrumentRequirements` chỉ nhận skill thuộc nhóm Instrument; `vocalRequirements` nhận skill thuộc
  mọi nhóm còn lại (Vocal, Solo, Psalm, Conducting support). Sai nhóm → 400 `SONG_SKILL_REQUIREMENT_CATEGORY_INVALID`.

---

## 7. Music materials — `api/music-materials` · xem: mọi role · upload / sửa / xoá: `ChoirDirector` · tài liệu của tôi + đánh dấu tiến độ: `ChoirMember`

Tư liệu của bài hát: bản nhạc, lời, audio mẫu, tài liệu tập (UC-21 / FE-28); ca viên tìm, lọc
(UC-07 / UC-07E) và đánh dấu tiến độ học (UC-08 / FE-09).

### `MusicMaterialDto`

```json
{
  "id": "guid",
  "songId": "guid",
  "materialType": "SampleAudio",
  "title": "Audio mẫu bè Tenor",
  "fileName": "kinh-hoa-binh-tenor.mp3",
  "fileSizeBytes": 2048000,
  "targetSkillId": "guid",
  "targetSkillName": "Tenor",
  "fileUrl": "https://...signed...",
  "createdAt": "2026-10-02T08:00:00Z"
}
```

### `MusicMaterialDetailDto` (chỉ `/mine`)

Đủ các field của `MusicMaterialDto`, thêm tiến độ học **của chính ca viên đang gọi**:

```json
{
  "...": "các field của MusicMaterialDto",
  "learningStatus": "NeedsPractice",
  "learningUpdatedAt": "2026-10-03T09:15:00Z"
}
```

- `learningStatus`: `NotStarted` | `NeedsPractice` | `Learned`. Chưa đánh dấu lần nào → `NotStarted`,
  `learningUpdatedAt` = `null`.

### `MaterialLearningProgressDto` (kết quả đánh dấu)

```json
{ "materialId": "guid", "status": "Learned", "updatedAt": "2026-10-03T09:15:00Z" }
```

### `MaterialLearningProgressDetailDto` (ca trưởng xem tiến độ)

```json
{ "memberId": "guid", "fullName": "Nguyễn Văn An", "status": "NotStarted", "updatedAt": null }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/music-materials` | `multipart/form-data`: `songId`, `title`, `materialType`, `targetSkillId?`, `file` | 200 `MusicMaterialDto` | 400 `VALIDATION_FAILED`, `MATERIAL_FILE_REQUIRED`, `MATERIAL_FILE_TYPE_NOT_ALLOWED` · 404 `SONG_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE` · 413 `MATERIAL_FILE_TOO_LARGE` · 502 `EXTERNAL_STORAGE_FAILED` |
| GET | `/api/music-materials?songId=&pageNumber=1&pageSize=20` | — | 200 `PagedList<MusicMaterialDto>` | 400 `VALIDATION_FAILED` (thiếu `songId`) · 404 `SONG_NOT_FOUND` |
| GET | `/api/music-materials/mine?keyword=&songId=&liturgicalSeasonId=&skillId=&materialType=&learningStatus=&pageNumber=1&pageSize=20` · chỉ `ChoirMember` | — | 200 `PagedList<MusicMaterialDetailDto>` — tài liệu cho mọi người + tài liệu cho kỹ năng **đã duyệt** của ca viên (UC-07); bộ lọc xem bên dưới (UC-07E) | 404 `MEMBER_NOT_FOUND`, `SONG_NOT_FOUND` |
| PUT | `/api/music-materials/{id}/learning-progress` · chỉ `ChoirMember` | `{ "status": "Learned" }` | 200 `MaterialLearningProgressDto` | 400 `VALIDATION_FAILED` (`errors.status`: `MATERIAL_LEARNING_STATUS_INVALID`) · 404 `MEMBER_NOT_FOUND`, `MATERIAL_NOT_FOUND` |
| GET | `/api/music-materials/{id}/learning-progress?status=&pageNumber=1&pageSize=20` · chỉ `ChoirDirector` | — | 200 `PagedList<MaterialLearningProgressDetailDto>` | 404 `MATERIAL_NOT_FOUND` |
| PUT | `/api/music-materials/{id}` | `{ "title", "targetSkillId" }` | 200 `MusicMaterialDto` | 400 `VALIDATION_FAILED` · 404 `MATERIAL_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE` |
| DELETE | `/api/music-materials/{id}` | — | 204 | 404 `MATERIAL_NOT_FOUND` |

- `materialType`: `SheetMusic` | `Lyrics` | `SampleAudio` | `RehearsalMaterial` (gửi tên hoặc số).
- Định dạng theo loại, tối đa **20 MB**:

  | `materialType` | Đuôi file |
  |---|---|
  | `SheetMusic`, `Lyrics` | `.pdf`, `.png`, `.jpg` |
  | `SampleAudio` | `.mp3`, `.m4a`, `.wav` |
  | `RehearsalMaterial` | cả hai nhóm trên |

  Server chỉ xét đuôi file và dung lượng thật, bỏ qua `Content-Type` client gửi.
- `targetSkillId` (tuỳ chọn): bè / nhạc cụ mà tư liệu dành cho, ví dụ audio riêng bè Tenor.
- `fileUrl` là link có hạn (`Cloudinary__SignedUrlExpiryMinutes`); hết hạn thì gọi lại GET để lấy link mới.
  Đừng lưu link này lại.
- Danh sách sắp theo `materialType` rồi `title`.
- `PUT` chỉ sửa `title` và `targetSkillId` (gửi `null` để bỏ gắn bè); `id`, file và `materialType` giữ nguyên,
  nên tiến độ học của ca viên không mất. Muốn thay file hoặc đổi loại: xoá rồi upload lại.
- `502 EXTERNAL_STORAGE_FAILED`: Cloudinary lỗi hoặc không phản hồi, không có gì được lưu; thử lại sau.
- `DELETE` là xoá mềm, không khôi phục: tư liệu biến khỏi danh sách, file vẫn giữ trên storage.

**`/mine` — tìm và lọc (UC-07E).** Mọi tham số tuỳ chọn, kết hợp theo AND:

| Tham số | Ý nghĩa |
|---|---|
| `keyword` | Tìm trong tên bài hát **hoặc** tên tư liệu; bỏ khoảng trắng hai đầu, chuỗi rỗng coi như không lọc |
| `songId` | Một bài hát (`404 SONG_NOT_FOUND` nếu bài không tồn tại hoặc đã xoá) |
| `liturgicalSeasonId` | Bài hát được phân loại vào mùa phụng vụ này |
| `skillId` | Chỉ tư liệu dành **đúng** bè / nhạc cụ này — không kèm tư liệu chung |
| `materialType` | Một loại tư liệu |
| `learningStatus` | Tiến độ của chính ca viên; `NotStarted` = những tư liệu chưa đánh dấu |

Danh sách `/mine` sắp theo tên bài hát, rồi `materialType`, rồi `title`.

**Đánh dấu tiến độ (UC-08).**

- `status` chỉ nhận `Learned` hoặc `NeedsPractice`. `NotStarted` không đặt được — đó là trạng thái khi chưa
  đánh dấu lần nào.
- Lần đầu tạo mới, các lần sau ghi đè; gọi lại với cùng giá trị cho cùng kết quả.
- Chỉ đánh dấu được tư liệu có trong `/mine` của ca viên. Tư liệu của bè khác, đã xoá hoặc không tồn tại
  đều trả `404 MATERIAL_NOT_FOUND`.

**Ca trưởng xem tiến độ một tư liệu.**

- Danh sách gồm ca viên **đang hoạt động** cần học tư liệu đó: tư liệu chung → mọi ca viên; tư liệu theo bè /
  nhạc cụ → ca viên đã được **duyệt** kỹ năng đó. Sắp theo `fullName`.
- Ai chưa đánh dấu hiện `NotStarted`. Lọc `status=NotStarted` để xem ai chưa bắt đầu.
- Tư liệu đã xoá hoặc không tồn tại → `404 MATERIAL_NOT_FOUND`.

---

## 8. SignalR — `/hubs/notifications`

Chỉ server → client; client không gọi method nào trên hub.

- Kết nối: `/hubs/notifications?access_token=<accessToken>` (WebSocket không gửi được header).
  Thiếu / sai token → bị từ chối kết nối.
- Sự kiện lắng nghe: **`ReceiveNotificationAsync`**, tham số là một `NotificationDto`
  (enum dạng tên, như REST).
- Thông báo được gửi riêng tới từng user nhận, không broadcast.
- Access token hết hạn: refresh rồi kết nối lại với token mới.

```js
const conn = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/notifications`, { accessTokenFactory: () => getAccessToken() })
  .withAutomaticReconnect()
  .build();
conn.on("ReceiveNotificationAsync", (n) => { /* n: NotificationDto */ });
await conn.start();
```

---

## 9. Lookups — `api/lookups` · mọi role đã đăng nhập

Danh mục cho dropdown. Chỉ trả dòng **đang hoạt động**; Admin tắt một dòng thì nó biến khỏi
danh sách (dữ liệu cũ vẫn trỏ tới nó). Không phân trang — trả mảng.

| Route | Phần tử | Sắp theo |
|---|---|---|
| `GET /api/lookups/mass-types` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/ceremony-types` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/event-categories` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/song-themes` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/skill-categories` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/liturgical-seasons` | `{ "id", "name", "startDate", "endDate", "colorHex" }` | `startDate` |
| `GET /api/lookups/liturgical-slots` | `{ "id", "name", "defaultOrder" }` | `defaultOrder` (thứ tự trong lễ) |
| `GET /api/lookups/worship-locations` | `{ "id", "name", "address" }` | tên |
| `GET /api/lookups/skills?categoryId=` | `{ "id", "categoryId", "name", "description" }` | tên |

- `description`, `address`, `colorHex` có thể `null`.
- `skills`: `categoryId` tuỳ chọn; bỏ qua kỹ năng thuộc nhóm đã tắt.
- Id của 5 nhóm kỹ năng là cố định trên mọi môi trường:

| Nhóm | Id |
|---|---|
| Vocal | `c4829abd-bb1d-4c6c-b401-9a177a88e66a` |
| Instrument | `0904ad8b-87f2-46c5-9938-f6cfbf0fa70b` |
| Solo | `550a67bb-0393-4bc4-8fe0-d7a453871f81` |
| Psalm | `a1c322c5-14af-4ca4-892a-b6e8d0eacbbd` |
| Conducting support | `e2722720-3745-48ea-9e8e-14ec7a63907d` |

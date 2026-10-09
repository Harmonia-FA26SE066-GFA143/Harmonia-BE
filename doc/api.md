# Harmonia API

Tài liệu cho FE (web) và mobile. Chỉ liệt kê endpoint **đã có trong code**; thêm controller
mới thì cập nhật file này trong cùng PR.

- Base URL dev: `http://localhost:5259` · `https://localhost:7112`
- Swagger (dev): `/swagger`
- Định dạng: JSON, tên field `camelCase`. Enum truyền bằng **tên** (`"Android"`, không phải `0`).
- Thời gian:
  - `DateTime` (mốc thời gian: `createdAt`, `publishedAt`, `accessTokenExpiresAt`, `startTime`…) là
    UTC ISO-8601, **luôn có hậu tố `Z`** — `"2026-10-07T01:00:00Z"`. Client tự đổi sang giờ địa phương
    khi hiển thị. Request gửi `DateTime` phải có `Z` hoặc offset.
  - `DateOnly` (`yyyy-MM-dd`) và `TimeOnly` (`HH:mm:ss`) là ngày/giờ trên lịch tại Việt Nam, không mang
    múi giờ — `eventDate`, `time`, `dateOfBirth`. Không đổi múi giờ, không parse bằng `new Date()`.
  - "Hôm nay" (sự kiện đã qua, lịch sắp tới, ngày sinh không ở tương lai) tính theo giờ Việt Nam (UTC+7).

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
| Chưa đổi mật khẩu lần đầu (tài khoản Admin vừa tạo) | 403 | `AUTH_PASSWORD_CHANGE_REQUIRED` → màn đổi mật khẩu |
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
`reset-password`. Mật khẩu đầu tiên do server sinh khi Admin tạo tài khoản (`POST /api/users`).

---

## 2. Auth — `api/auth`

Không có đăng ký công khai; tài khoản do Admin tạo.

### `LoginResponse` (dùng chung cho login / google / refresh)

```json
{
  "accessToken": "eyJ...",
  "accessTokenExpiresAt": "2026-10-01T08:30:00Z",
  "refreshToken": "q8x...",
  "user": { "id": "guid", "email": "a@b.com", "fullName": "Nguyễn Văn A", "roleName": "ChoirMember", "isPasswordChangeRequired": false }
}
```

`user.isPasswordChangeRequired = true` → tài khoản vừa được Admin tạo, mật khẩu do server sinh đã gửi qua email: client đưa người
dùng tới màn đổi mật khẩu (`POST /api/auth/change-password`) trước khi cho dùng tiếp. Đổi hoặc đặt lại mật khẩu xong
cờ về `false`.

Khi cờ là `true`, server trả **403 `AUTH_PASSWORD_CHANGE_REQUIRED`** cho mọi endpoint cần đăng nhập, **trừ**
`change-password`, `logout`, `logout-all`, `GET/PUT me` (endpoint anonymous như `refresh` không bị ảnh hưởng).
Đổi mật khẩu xong mọi phiên bị thu hồi → đăng nhập lại bằng mật khẩu mới là hết bị chặn.

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

### `GET /api/auth/me` · `PUT /api/auth/me` — đã đăng nhập, mọi role

Tài khoản của chính người gọi. `GET` trả `UserDto` (mục 3). `PUT` nhận `{ "fullName", "phone" }`, cả hai tuỳ chọn
(`fullName` ≤ 100, `phone` ≤ 20 ký tự, cắt khoảng trắng; gửi rỗng / `null` là xoá) và trả `UserDto`.
Gửi đủ cả hai trường — trường bỏ trống bị xoá. Email và role không đổi được ở đây.

| HTTP | Kết quả |
|---|---|
| 200 | `UserDto` |
| 400 | `VALIDATION_FAILED` |
| 401 | Chưa đăng nhập |

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
{ "id": "guid", "email": "a@b.com", "fullName": "Nguyễn Văn A", "phone": "0900000000", "roleName": "ChoirDirector", "isActive": true, "isPasswordChangeRequired": false }
```

`fullName` và `phone` có ở **mọi role** (lưu trên `User`). `fullName` có thể là `""` (Admin bỏ trống) — client
hiển thị email thay tên. `phone` có thể `null`.

| Method | Route | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/users` | query `keyword`, `roleName`, `isActive`, `pageNumber`, `pageSize` | 200 `PagedList<UserDto>` | — |
| GET | `/api/users/{id}` | — | 200 `UserDto` | 404 `USER_NOT_FOUND` |
| POST | `/api/users` | `{ "email", "fullName?", "phone?", "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `ROLE_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PUT | `/api/users/{id}` | `{ "email", "fullName?", "phone?" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PATCH | `/api/users/{id}/activate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_ALREADY_ACTIVE` |
| PATCH | `/api/users/{id}/deactivate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_ALREADY_INACTIVE`, `USER_LAST_ADMIN` |
| PUT | `/api/users/{id}/role` | `{ "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND`, `ROLE_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_LAST_ADMIN` |

- `GET /api/users`: `keyword` khớp một phần email; các bộ lọc kết hợp AND; sắp theo email.
- Bắt buộc: `email`, `roleName`. Tuỳ chọn: `fullName` (≤ 100, bỏ trống → `""`),
  `phone` (≤ 20, bỏ trống → `null`); khoảng trắng đầu/cuối bị cắt. `PUT` thay toàn bộ: trường bỏ trống bị xoá.
- Admin **không** nhập mật khẩu: server tự sinh mật khẩu đầu tiên (12 ký tự ngẫu nhiên), gửi email chứa email đăng
  nhập + mật khẩu tới người dùng và đặt `isPasswordChangeRequired = true`. Mật khẩu không bao giờ có trong response.
  Gửi mail thất bại thì tài khoản **vẫn được tạo** (200) — người dùng lấy lại quyền vào qua `forgot-password`.
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

### `MemberProfileSummaryDto` / `MemberProfileDetailDto`

`MemberProfileSummaryDto` = `MemberProfileDto` + `approvedSkills`.
`MemberProfileDetailDto` = `MemberProfileSummaryDto` + `roleName`.

```json
{
  "...": "các field của MemberProfileDto",
  "roleName": "ChoirMember",
  "approvedSkills": [
    { "skillId": "guid", "skillName": "Tenor", "categoryId": "guid", "categoryName": "Vocal", "level": "Intermediate" }
  ]
}
```

`approvedSkills` chỉ gồm kỹ năng **đã duyệt** và skill đang hoạt động; `level` có thể `null`.
Chưa có kỹ năng nào → `[]`.

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/member-profiles/me` | `ChoirMember` | — | 200 `MemberProfileDetailDto` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/member-profiles/me` | `ChoirMember` | `{ "fullName", "phone", "dateOfBirth" }` | 200 `MemberProfileDto` | 400 `VALIDATION_FAILED` · 404 `MEMBER_NOT_FOUND` |
| GET | `/api/member-profiles` | `ChoirDirector` | query `keyword`, `status`, `skillId`, `pageNumber`, `pageSize` | 200 `PagedList<MemberProfileSummaryDto>` | — |
| GET | `/api/member-profiles/{id}` | `ChoirDirector` | — | 200 `MemberProfileDto` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/member-profiles/{id}` | `ChoirDirector` | `{ "phone", "dateOfBirth", "joinedDate", "status" }` | 200 `MemberProfileDto` | 400 `VALIDATION_FAILED`, `MEMBER_JOINED_DATE_IN_FUTURE` · 404 `MEMBER_NOT_FOUND` |
| GET | `/api/member-profiles/me/history` | `ChoirMember` | query `liturgicalSeasonId`, `fromDate`, `toDate`, `pageNumber`, `pageSize` | 200 `PagedList<ParticipationHistoryDto>` | 404 `MEMBER_NOT_FOUND` |

- `PUT` thay **toàn bộ** các field trong body: field bỏ trống / `null` sẽ bị xoá giá trị
  (`phone`, `dateOfBirth`). Gửi lại giá trị cũ nếu không muốn đổi.
- `PUT .../me`: `fullName` bắt buộc (≤ 100 ký tự), đổi tên hiển thị của chính tài khoản đó.
  `phone` ≤ 20 ký tự. `dateOfBirth` không được ở tương lai.
- `PUT .../{id}`: `joinedDate` bắt buộc, không ở tương lai. Ca trưởng **không** sửa tên ca viên
  (tên do ca viên tự sửa, hoặc Admin sửa qua `PUT /api/users/{id}`).
- `GET /api/member-profiles`: `keyword` khớp một phần tên hoặc email; `skillId` chỉ giữ ca viên đã
  được **duyệt** kỹ năng đó (lấy id từ `GET /api/lookups/skills`; skill đã tắt → danh sách rỗng); các bộ lọc kết hợp AND; sắp theo tên.
  Dùng để chọn người khi phân công theo bè / nhạc cụ.
- Role khác gọi vào → `403` body rỗng.

### Lịch sử tham gia & luyện tập của tôi — `ChoirMember` (UC-11 / FE-14)

`ParticipationHistoryDto`:

```json
{
  "eventId": "guid", "eventDate": "2026-09-27", "title": "Chúa nhật XXVI Thường niên",
  "liturgicalSeasonName": "Mùa Thường niên",
  "participationStatus": "Confirmed",
  "servedSkills": ["Tenor"],
  "rehearsalsHeld": 2, "rehearsalsAttended": 1,
  "assignmentsTotal": 3, "assignmentsPassed": 2
}
```

- Chỉ gồm sự kiện `Published` **trước hôm nay** (giờ Việt Nam) mà ca viên có liên quan: được mời xác nhận tham gia,
  hoặc có tên trên phân công đã chốt. Sắp mới nhất trước.
- Bộ lọc tuỳ chọn, kết hợp AND: `liturgicalSeasonId` (id từ `GET /api/lookups/liturgical-seasons`), `fromDate`, `toDate`
  (`yyyy-MM-dd`, bao gồm hai đầu).
- `participationStatus` `null` khi ca viên không được mời. `servedSkills`: kỹ năng phục vụ trên phân công **đã chốt**
  (không trùng tên); `[]` nếu không phục vụ.
- `rehearsalsAttended` tính `Present` + `Late`. `assignmentsPassed` xét bản thu **mới nhất** của từng bài.
  Chi tiết từng bản thu: `GET /api/practice-submissions/mine`.

---

## 4a. Member skills — `api/member-skills` · khai báo + theo dõi: `ChoirMember` (UC-03)

### `MemberSkillDto`

```json
{
  "id": "guid",
  "skillId": "guid",
  "skillName": "Tenor",
  "categoryId": "guid",
  "categoryName": "Vocal",
  "level": "Intermediate",
  "status": "Pending",
  "declaredAt": "2026-10-05T08:00:00Z",
  "approvedAt": null,
  "rejectReason": null
}
```

`level`: `Beginner` | `Intermediate` | `Advanced` | `null`. `status`: `Pending` | `Approved` | `Rejected`.
`approvedAt` là lúc ca trưởng duyệt **hoặc** từ chối; `rejectReason` chỉ có khi `Rejected`.

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| POST | `/api/member-skills` | `ChoirMember` | `{ "skillId", "level" }` | 200 `MemberSkillDto` | 400 `VALIDATION_FAILED` · 404 `MEMBER_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `MEMBER_NOT_ACTIVE`, `SKILL_INACTIVE`, `MEMBER_SKILL_ALREADY_DECLARED` |
| GET | `/api/member-skills/mine` | `ChoirMember` | query `status`, `pageNumber`, `pageSize` | 200 `PagedList<MemberSkillDto>` | 404 `MEMBER_NOT_FOUND` |
| GET | `/api/member-skills/{id}` | `ChoirMember` | — | 200 `MemberSkillDto` | 404 `MEMBER_NOT_FOUND`, `MEMBER_SKILL_NOT_FOUND` |

- Ca viên luôn khai cho **chính mình** — không có `memberId` trong body. Kỹ năng mới vào trạng thái `Pending`.
- `skillId` bắt buộc, lấy từ `GET /api/lookups/skills`. `level` tuỳ chọn.
- Skill bị tắt, hoặc thuộc nhóm kỹ năng bị tắt → 409 `SKILL_INACTIVE`. Ca viên `Inactive` / `Left` → 409 `MEMBER_NOT_ACTIVE`.
- Đã có bản khai `Pending` hoặc `Approved` cho skill đó → 409 `MEMBER_SKILL_ALREADY_DECLARED`.
- `status` (tuỳ chọn): `Pending` | `Approved` | `Rejected` — bỏ trống thì trả mọi trạng thái.
- `GET /{id}` chỉ trả bản khai của chính người gọi; bản khai của ca viên khác → 404 `MEMBER_SKILL_NOT_FOUND`.
- Bị **từ chối** thì được khai lại: bản `Rejected` giữ nguyên làm lịch sử, hệ thống thêm bản mới `Pending`.
- `GET .../mine` trả **mọi** bản khai kể cả lịch sử bị từ chối, mới nhất trước.
- Role khác gọi vào → `403` body rỗng.

### Ca trưởng duyệt kỹ năng — `ChoirDirector` (UC-19)

`MemberSkillDetailDto` = `MemberSkillDto` + `memberId`, `memberFullName`.

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/member-skills/pending` | `ChoirDirector` | query `pageNumber`, `pageSize` | 200 `PagedList<MemberSkillDetailDto>` | — |
| PATCH | `/api/member-skills/{id}/approve` | `ChoirDirector` | — | 200 `MemberSkillDetailDto` | 404 `MEMBER_SKILL_NOT_FOUND` · 409 `MEMBER_SKILL_ALREADY_REVIEWED` |
| PATCH | `/api/member-skills/{id}/reject` | `ChoirDirector` | `{ "reason" }` | 200 `MemberSkillDetailDto` | 400 `MEMBER_SKILL_REJECT_REASON_REQUIRED`, `VALIDATION_FAILED` · 404 `MEMBER_SKILL_NOT_FOUND` · 409 `MEMBER_SKILL_ALREADY_REVIEWED` |

- `pending` trả bản khai `Pending` của mọi ca viên, cũ nhất trước.
- `reason` bắt buộc, tối đa 500 ký tự.
- Chỉ duyệt/từ chối được bản `Pending`; bản đã duyệt hoặc đã từ chối → 409.
- Duyệt hoặc từ chối xong, ca viên nhận notification `type = SkillReview`,
  `referenceType = "MemberSkill"`, `referenceId` = id bản khai (S-05).

---

## 4b. Lịch phụng vụ & sự kiện — `api/liturgical-days`, `api/liturgical-events` · role `ParishPriest` · tiến độ chuẩn bị: `ChoirDirector` · lịch sắp tới `api/schedule`: mọi role (UC-12 / UC-04 / UC-15 / UC-30)

### `LiturgicalDayDto`

```json
{ "date": "2026-12-25", "celebrationName": "Lễ Giáng Sinh", "rank": "Lễ trọng", "seasonName": "Giáng Sinh" }
```

### `LiturgicalEventDto`

```json
{
  "id": "guid",
  "eventDate": "2026-12-25",
  "time": "08:00:00",
  "liturgicalSeasonId": "guid",
  "massTypeId": "guid",
  "ceremonyTypeId": null,
  "categoryId": "guid",
  "locationId": "guid",
  "locationName": "Nhà thờ chính",
  "title": "Thánh lễ Giáng Sinh",
  "specialRequirements": null,
  "status": "Draft",
  "publishedAt": null
}
```

`status`: `Draft` | `Published` | `Cancelled`. `LiturgicalEventSummaryDto` = `{ id, eventDate, time, title, locationName, status }`.
`RehearsalSummaryDto` = `{ id, startTime, endTime, locationName, note }`.

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/liturgical-days/{date}` (`date` dạng `yyyy-MM-dd`) | — | 200 `LiturgicalDayDto` | 404 `CALENDAR_DAY_NOT_FOUND` |
| POST | `/api/liturgical-days/import` | `multipart/form-data`: `file` (.ics) | 200 số ngày mới thêm (`int`) | 400 `CALENDAR_FILE_REQUIRED`, `CALENDAR_FILE_TYPE_NOT_ALLOWED`, `CALENDAR_FILE_INVALID` · 413 `CALENDAR_FILE_TOO_LARGE` |
| GET | `/api/liturgical-events?fromDate=&toDate=&status=&pageNumber=&pageSize=` | — | 200 `PagedList<LiturgicalEventDto>` | — |
| GET | `/api/liturgical-events/{id}` | — | 200 `LiturgicalEventDto` | 404 `EVENT_NOT_FOUND` |
| POST | `/api/liturgical-events` | `{ "eventDate", "time", "liturgicalSeasonId?", "massTypeId?", "ceremonyTypeId?", "categoryId?", "locationId", "title?", "specialRequirements?" }` | 200 `LiturgicalEventDto` | 400 `VALIDATION_FAILED` (`EVENT_TYPE_REQUIRED`) · 409 `EVENT_SLOT_TAKEN` |
| PUT | `/api/liturgical-events/{id}` | như `POST` | 200 `LiturgicalEventDto` | 400 `VALIDATION_FAILED` (`EVENT_TYPE_REQUIRED`) · 404 `EVENT_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_SLOT_TAKEN` |
| PATCH | `/api/liturgical-events/{id}/publish` | — | 200 `LiturgicalEventDto` | 404 `EVENT_NOT_FOUND` · 409 `EVENT_ALREADY_PUBLISHED`, `EVENT_CANCELLED` |
| PATCH | `/api/liturgical-events/{id}/cancel` | — | 200 `LiturgicalEventDto` | 404 `EVENT_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED` |
| GET | `/api/schedule/events` · mọi role | — | 200 `LiturgicalEventSummaryDto[]` | — |
| GET | `/api/schedule/rehearsals` · mọi role | — | 200 `RehearsalSummaryDto[]` | — |
| GET | `/api/liturgical-events/{id}/preparation-progress` · `ChoirDirector` | — | 200 `EventPreparationProgressDto[]` | 404 `EVENT_NOT_FOUND` |
| GET | `/api/liturgical-events/{id}/preparation-status` | — | 200 `EventPreparationStatusDto` | 404 `EVENT_NOT_FOUND` |

**Ngày phụng vụ (UC-12 / FE-15)**

- Ngày phụng vụ là bản cache của lịch Công giáo, nạp bằng file `.ics`. Chỉ nhận đuôi `.ics`, tối đa **2 MB**;
  server chỉ xét đuôi và dung lượng thật, bỏ qua `Content-Type`.
- Import chỉ **thêm** ngày chưa có; ngày đã có giữ nguyên, không ghi đè. Kết quả là số ngày mới thêm (`0` nếu
  file không có ngày nào mới). File không đọc được hoặc không có ngày nào → 400 `CALENDAR_FILE_INVALID`.
- `rank`, `seasonName` có thể `null`.

**Sự kiện (UC-12 / FE-16)**

- Phải có `massTypeId` **hoặc** `ceremonyTypeId`, thiếu cả hai → 400 `VALIDATION_FAILED` với
  `errors` chứa `EVENT_TYPE_REQUIRED`. `title` tối đa 200, `specialRequirements` tối đa 1000 ký tự.
- Một ngày có nhiều sự kiện được, nhưng trùng cả ngày + giờ + địa điểm → 409 `EVENT_SLOT_TAKEN`.
- Sự kiện mới luôn ở `Draft`. Công bố (`publish`) chuyển sang `Published`, ghi `publishedAt`, rồi gửi
  notification `type = EventPublished` cho mọi ca trưởng và ca viên đang hoạt động,
  `referenceType = "LiturgicalEvent"`, `referenceId` = id sự kiện (S-05).
- Mỗi sự kiện công bố riêng (D6). Ca viên chỉ thấy sự kiện `Published`.
- Danh sách (`GET`) trả sự kiện **mọi trạng thái** (kể cả `Draft`, `Cancelled`), sắp theo ngày rồi giờ; các bộ lọc
  `fromDate`, `toDate` (bao gồm hai đầu, `yyyy-MM-dd`) và `status` kết hợp AND, đều tuỳ chọn.
- Sửa (`PUT`) được khi sự kiện `Draft` hoặc `Published`; gửi đủ mọi trường như `POST`. Sửa không đổi trạng thái,
  không gửi thông báo. Trùng ngày + giờ + địa điểm với sự kiện **khác** → 409 `EVENT_SLOT_TAKEN`.
- Huỷ (`cancel`) chuyển sang `Cancelled`, không hoàn tác được; sự kiện đã qua (trước hôm nay theo giờ Việt Nam)
  → 409 `EVENT_ALREADY_PASSED`. Nếu sự kiện đang `Published`, gửi notification `type = EventCancelled` cho mọi ca
  trưởng và ca viên đang hoạt động (`referenceType = "LiturgicalEvent"`); huỷ `Draft` thì không gửi.
- Hiện tại `locationName` trong response của `POST` và `publish` là `null` (server chưa nạp địa điểm);
  lấy tên địa điểm từ `GET /api/lookups/worship-locations` theo `locationId`.

**Lịch sắp tới (UC-04 / FE-05)**

- `events`: sự kiện `Published` từ hôm nay trở đi, sắp theo ngày rồi giờ.
- `rehearsals`: buổi tập bắt đầu từ thời điểm gọi trở đi, sắp theo giờ bắt đầu. `locationName`, `note` có thể `null`.
- Không phân trang — trả mảng; không có gì → `[]`.

**Tiến độ chuẩn bị cho sự kiện (UC-30 / FE-46)**

`EventPreparationProgressDto` = `{ memberId, fullName, participationStatus, rehearsalsHeld, rehearsalsAttended, assignmentsTotal, assignmentsPassed, assignmentsOverdue }`.

- Mỗi dòng một ca viên đang hoạt động, sắp theo tên. Không phân trang. Xem được cả sự kiện đã qua.
- `participationStatus`: `Invited` | `Confirmed` | `Declined` | `Unsure`, hoặc `null` khi ca viên chưa được mời.
- `rehearsalsHeld`: số buổi tập / buổi chuẩn bị gắn với sự kiện **đã bắt đầu** (giống nhau ở mọi dòng);
  `rehearsalsAttended`: trong số đó, buổi ca viên được điểm danh `Present` hoặc `Late`.
- `assignmentsTotal`: số bài tập gắn với sự kiện mà ca viên nhận (cùng luật với `GET /api/practice-assignments/mine`);
  `assignmentsPassed` / `assignmentsOverdue` xét theo bản thu mới nhất, luật quá hạn giống `isOverdue`.
- Trả số thô; FE tự tính phần trăm (cẩn thận chia cho 0 khi `rehearsalsHeld` hoặc `assignmentsTotal` = 0).

**Tình trạng chuẩn bị của ca đoàn (UC-15 / FE-21) — cha xứ**

```json
{
  "eventId": "guid", "title": "Thánh lễ Giáng Sinh", "eventDate": "2026-12-24", "eventStatus": "Published",
  "songListStatus": "Approved",
  "participationInvited": 3, "participationConfirmed": 25, "participationDeclined": 2, "participationUnsure": 1,
  "rosterStatus": "Finalized", "rosterActiveAssignments": 18,
  "rosterShortages": [{ "songListItemId": "guid", "songTitle": "...", "skillId": "guid", "skillName": "Tenor", "requiredCount": 3, "assignedCount": 2 }],
  "rehearsalsTotal": 4, "rehearsalsHeld": 2,
  "attendanceExpected": 60, "attendancePresent": 51,
  "practiceExpected": 90, "practicePassed": 70, "practiceOverdue": 6
}
```

- Xem được mọi sự kiện, kể cả đã qua. Chỉ trả số tổng của cả ca đoàn; bảng từng ca viên là `preparation-progress` của ca trưởng.
- `songListStatus`: trạng thái phiên bản danh sách bài hát **mới nhất**; `null` nếu chưa đề xuất.
- `participation*`: đếm `EventParticipation` theo trạng thái; `participationInvited` = đã mời nhưng chưa trả lời.
- `rosterStatus`: `Draft` | `Suggested` | `Finalized`, `null` nếu chưa có phân công; `rosterActiveAssignments` không tính dòng đã bị thay.
- `rosterShortages`: giống `GET /api/service-rosters/shortages?eventId=`; `null` khi chưa có danh sách bài hát được duyệt (chưa biết cần bao nhiêu người), `[]` khi đủ người.
- `attendanceExpected` = `rehearsalsHeld` × số ca viên đang hoạt động; `attendancePresent` = tổng lượt `Present` + `Late`.
- `practice*`: cộng dồn các cột `assignments*` của `preparation-progress` trên mọi ca viên đang hoạt động.

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
`PracticeFeedback` | `DirectorNote` | `SkillReview` | `EventCancelled`. `referenceType` + `referenceId` (nullable) cho biết bấm vào
thì mở màn nào.

| Method | Route | Thành công | Lỗi |
|---|---|---|---|
| GET | `/api/notifications?isRead=&pageNumber=1&pageSize=20` | 200 `PagedList<NotificationDto>` | — |
| GET | `/api/notifications/unread-count` | 200, body là số nguyên (`3`) | — |
| PUT | `/api/notifications/{id}/read` | 204 | 404 `NOTIFICATION_NOT_FOUND` |
| PUT | `/api/notifications/read-all` | 204 | — |

- `isRead` tuỳ chọn: `false` chỉ lấy thông báo chưa đọc, `true` chỉ lấy đã đọc, bỏ trống lấy tất cả.
- `read-all` đánh dấu mọi thông báo chưa đọc của người gọi là đã đọc (`readAt` = lúc gọi); thông báo đã đọc
  giữ nguyên `readAt` cũ. Không còn gì chưa đọc thì vẫn trả 204.

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

## 7a. Song list items — `api/song-list-items` · xem: mọi role · sửa: `ChoirDirector` (UC-24)

### Nhân sự cần cho mỗi bài — `SongPersonnelRequirementDto` / `UpdateSongPersonnelRequirementsRequest`

```json
[{ "skillId": "guid", "skillName": "Soprano", "skillCategoryId": "guid", "requiredCount": 3 }]
```

```json
{ "requirements": [{ "skillId": "guid", "requiredCount": 3 }] }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/song-list-items/{id}/personnel-requirements` | — | 200 `SongPersonnelRequirementDto[]` | 404 `SONG_LIST_ITEM_NOT_FOUND` |
| PUT | `/api/song-list-items/{id}/personnel-requirements` | `UpdateSongPersonnelRequirementsRequest` | 200 `SongPersonnelRequirementDto[]` | 400 `VALIDATION_FAILED` · 404 `SONG_LIST_ITEM_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE`, `SONG_LIST_NOT_LATEST_VERSION`, `ROSTER_ALREADY_FINALIZED` |

- `PUT` **thay cả bộ**: skill nào không gửi lên thì bị gỡ. Gửi `[]` để gỡ hết.
- `requiredCount` từ 1 đến 50 (`errors.<field>` chứa `PERSONNEL_REQUIRED_COUNT_INVALID`); skill trùng trong
  danh sách → `PERSONNEL_REQUIREMENT_DUPLICATE`. Cả hai bắt ở validator, trả 400 `VALIDATION_FAILED`.
- Nhận skill thuộc mọi nhóm (bè, nhạc cụ, solo, xướng đáp ca…). Skill **thêm mới** phải đang hoạt động;
  skill đã có từ trước vẫn giữ được dù sau đó bị vô hiệu hoá.
- Chỉ sửa được bài thuộc **phiên bản mới nhất** của danh sách, ở trạng thái nào cũng được, và khi bảng
  phân công của sự kiện chưa chốt.
- Ca viên chỉ xem được bài thuộc danh sách `Approved` của sự kiện `Published`; còn lại trả 404 như không tồn tại.
- Kết quả sắp theo `skillName`.

---

## 7b. Service rosters — `api/service-rosters` · role `ChoirDirector` (UC-25 / UC-25a / UC-25b / UC-26 / UC-27)

### `RosterSuggestionResponse`

```json
{
  "rosterId": "guid",
  "eventId": "guid",
  "status": "Suggested",
  "generatedAt": "2026-10-06T08:00:00Z",
  "isAiGenerated": true,
  "assignments": [{
    "id": "guid", "memberId": "guid", "memberName": "Nguyễn Văn An",
    "skillId": "guid", "skillName": "Soprano",
    "songListItemId": "guid", "songTitle": "Hãy Nâng Tâm Hồn", "source": "Suggested"
  }],
  "shortages": [{
    "songListItemId": "guid", "songTitle": "Hãy Nâng Tâm Hồn",
    "skillId": "guid", "skillName": "Guitar", "requiredCount": 2, "assignedCount": 1
  }]
}
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/service-rosters/suggestions` | `{ "eventId": "guid" }` | 200 `RosterSuggestionResponse` | 400 `VALIDATION_FAILED` · 404 `EVENT_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `ROSTER_NO_PERSONNEL_REQUIREMENT`, `ROSTER_ALREADY_FINALIZED` |
| GET | `/api/service-rosters?eventId=` | — | 200 `ServiceRosterDto` (assignment `Active` + shortage; `shortages` rỗng nếu sự kiện không còn song list đã duyệt) | 404 `EVENT_NOT_FOUND`, `ROSTER_NOT_FOUND` (chưa có roster — gọi `suggestions` hoặc thêm tay) |
| GET | `/api/service-rosters/shortages?eventId=` | — | 200 `RosterShortageDto[]` | 404 `EVENT_NOT_FOUND` · 409 `ROSTER_SONG_LIST_NOT_APPROVED` |
| POST | `/api/service-rosters/assignments` | `{ "eventId", "songListItemId", "skillId", "memberId" }` | 200 `RosterAssignmentDto` | 400 `VALIDATION_FAILED` · 404 `EVENT_NOT_FOUND`, `PERSONNEL_REQUIREMENT_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `ASSIGNMENT_DUPLICATE`, `MEMBER_NOT_ACTIVE`, `ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ASSIGNMENT_MEMBER_NOT_CONFIRMED` |
| POST | `/api/service-rosters/assignments/{id}/replacement` | `{ "memberId" }` | 200 `RosterAssignmentDto` (dòng mới) | 400 `VALIDATION_FAILED` · 404 `ASSIGNMENT_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ASSIGNMENT_DUPLICATE`, `MEMBER_NOT_ACTIVE`, `ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ASSIGNMENT_MEMBER_NOT_CONFIRMED` |
| DELETE | `/api/service-rosters/assignments/{id}` | — | 204 | 404 `ASSIGNMENT_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED` |
| POST | `/api/service-rosters/{id}/finalization` | — | 200 `ServiceRosterDto` | 404 `ROSTER_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `MEMBER_NOT_ACTIVE`, `ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ASSIGNMENT_MEMBER_NOT_CONFIRMED` |
| POST | `/api/service-rosters/{id}/notifications` | `{ "memberIds": ["guid"] }` | 204 | 400 `VALIDATION_FAILED` · 404 `ROSTER_NOT_FOUND`, `ASSIGNMENT_NOT_FOUND` · 409 `EVENT_CANCELLED`, `EVENT_ALREADY_PASSED`, `ROSTER_NOT_FINALIZED` |

`RosterAssignmentDto` = một phần tử của `assignments` ở trên. `RosterShortageDto` = một phần tử của `shortages`.
`{id}` của `finalization` / `notifications` là `rosterId` (có trong `RosterSuggestionResponse` và `ServiceRosterDto.id`).

### `ServiceRosterDto` (đọc roster hiện tại / kết quả chốt)

```json
{
  "id": "guid",
  "eventId": "guid",
  "status": "Finalized",
  "generatedAt": "2026-10-06T08:00:00Z",
  "finalizedAt": "2026-10-06T09:00:00Z",
  "finalizedBy": "guid",
  "assignments": [ /* RosterAssignmentDto */ ],
  "shortages": [ /* RosterShortageDto */ ]
}
```

**Gợi ý (`suggestions`)**

- Ứng viên cho mỗi cặp (bài, skill): ca viên **đang hoạt động**, đã được **duyệt** skill đó và đã
  **xác nhận tham gia** (`Confirmed`) sự kiện. Không ai ngoài danh sách này được gợi ý.
- AI (Gemini) chọn trong ứng viên, ưu tiên level cao và người ít phục vụ trong 60 ngày gần nhất. Server
  kiểm lại mọi lựa chọn của AI, rồi tự bù chỗ còn trống theo cùng tiêu chí. AI lỗi hoặc quá 20 giây thì
  phần bù lo toàn bộ và `isAiGenerated = false`. Endpoint không trả lỗi vì AI.
- Một ca viên có thể phục vụ nhiều bài, và nhiều skill trong cùng một bài.
- Gọi lại được nhiều lần cho tới khi chốt: dòng `source = Suggested` cũ bị thay; dòng `Manual` (ca trưởng sửa
  tay) **được giữ** và tính là đã có người, nên chỉ gợi ý thêm cho phần còn thiếu.
- `assignments` là **toàn bộ** phân công hiện tại của sự kiện (gợi ý mới + dòng tay), sắp theo thứ tự bài,
  tên skill, tên ca viên.
- `shortages` là các cặp (bài, skill) vẫn thiếu người sau khi gợi ý, tính lại mỗi lần gọi, không lưu.
- Không gửi thông báo cho ca viên ở bước này (xem UC-27).

**Cảnh báo thiếu người (`shortages`, UC-25a)**

- Trả các cặp (bài, skill) của danh sách bài hát đã duyệt mà số người đang phân công < `requiredCount`,
  theo thứ tự bài. Đủ người hết → `[]`. Chưa có bảng phân công → mọi yêu cầu đều thiếu.
- Mọi dòng phân công còn hiệu lực đều tính là có người (cả gợi ý lẫn thêm tay). Tính lại mỗi lần gọi.

**Sửa tay (`assignments`, UC-25b)**

- Ca viên được thêm / thay vào phải **đang hoạt động**, đã được **duyệt** skill đó và đã **xác nhận tham gia**
  sự kiện. Đã có đúng (bài, skill, ca viên) đó trên bảng → 409 `ASSIGNMENT_DUPLICATE`.
- Thêm: cặp (`songListItemId`, `skillId`) phải là một yêu cầu nhân sự của danh sách đã duyệt, nếu không → 404
  `PERSONNEL_REQUIREMENT_NOT_FOUND`. Được thêm vượt `requiredCount`. Sự kiện chưa có bảng thì tự tạo bảng `Draft`.
  Dòng thêm tay có `source = Manual`.
- Thay: dòng cũ giữ lại làm lịch sử (không còn hiện trong `assignments`), dòng mới `Manual` cùng bài + skill.
  Gợi ý lại sau đó không gợi ý lại người đã bị thay ở vị trí ấy.
- Xoá: xoá hẳn dòng phân công.
- `{id}` phải là dòng còn hiệu lực; dòng đã bị thay hoặc không tồn tại → 404 `ASSIGNMENT_NOT_FOUND`.
- Sự kiện đã huỷ / đã qua, hoặc bảng đã chốt → 409, không sửa được nữa.

**Chốt phân công (`finalization`, UC-26)**

- Danh sách bài hát của sự kiện phải đã duyệt. Chốt xong thì không gợi ý / thêm / thay / xoá dòng, và không
  sửa nhân sự cần cho bài được nữa. Chưa có API bỏ chốt.
- **Còn thiếu người vẫn chốt được**: các cặp còn thiếu trả trong `shortages`.
- Kiểm lại từng dòng còn hiệu lực: ca viên phải còn **đang hoạt động**, còn skill **đã duyệt** và vẫn
  **xác nhận tham gia**. Dòng nào hỏng → 409 với mã tương ứng, `message` chứa id dòng đó; bảng **không** bị chốt.
  Ca trưởng thay / xoá dòng đó rồi chốt lại.
- `assignments` chỉ gồm dòng còn hiệu lực, sắp như `suggestions`.

**Gửi thông báo phân công (`notifications`, UC-27)**

- Bảng phải đã chốt, nếu không → 409 `ROSTER_NOT_FINALIZED`.
- `memberIds` có giá trị → gửi đúng những ca viên đó, kể cả người đã được báo (gửi lại). Có id không có dòng
  nào trên bảng → 404 `ASSIGNMENT_NOT_FOUND`, không gửi cho ai.
- `memberIds` rỗng / bỏ trống → gửi cho mọi ca viên còn dòng **chưa được báo**.
- Mỗi ca viên nhận **một** thông báo `AssignmentNotice` liệt kê các vị trí của mình (skill – bài),
  `referenceType = "LiturgicalEvent"`, `referenceId` = id sự kiện. Đẩy realtime qua SignalR như mọi thông báo.
- Các dòng đã báo được đánh dấu thời điểm gửi (chưa trả trong `RosterAssignmentDto`).

---

## 7c. Practice assignments — `api/practice-assignments` · tạo: `ChoirDirector` (UC-28) · nhận bài: `ChoirMember` (UC-09)

### `PracticeAssignmentDto`

```json
{
  "id": "guid",
  "eventId": "guid | null",
  "songId": "guid | null",
  "materialId": "guid | null",
  "title": "Tập bài Nhập lễ",
  "instruction": "Thu âm bè Soprano, đoạn điệp khúc | null",
  "scope": "SkillGroup",
  "dueDate": "2026-10-12T15:00:00Z",
  "skillIds": ["guid"],
  "memberIds": []
}
```

| Method | Path | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/practice-assignments` | `{ "title", "instruction?", "scope", "dueDate", "eventId?", "songId?", "materialId?", "skillIds?", "memberIds?" }` | 200 `PracticeAssignmentDto` | 400 `VALIDATION_FAILED` (`PRACTICE_DUE_DATE_IN_PAST`, `PRACTICE_TARGET_REQUIRED`) · 404 `EVENT_NOT_FOUND`, `SONG_NOT_FOUND`, `MATERIAL_NOT_FOUND`, `SKILL_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `EVENT_CANCELLED`, `SONG_INACTIVE`, `SKILL_INACTIVE` |

- `scope`: `All` | `SkillGroup` | `Individual`.
  - `All`: giao cho mọi ca viên **đang hoạt động**. `skillIds` / `memberIds` bị bỏ qua.
  - `SkillGroup`: `skillIds` bắt buộc. Người nhận là ca viên đang hoạt động đã được **duyệt** ít nhất một skill trong danh sách.
  - `Individual`: `memberIds` (id hồ sơ ca viên) bắt buộc. Ca viên không còn hoạt động → 404 `MEMBER_NOT_FOUND`.
- `title` tối đa 200 ký tự, `instruction` tối đa 1000. `dueDate` là UTC, phải sau thời điểm gửi.
- `eventId`, `songId`, `materialId` đều tuỳ chọn. Sự kiện đã huỷ → 409 `EVENT_CANCELLED`. Tư liệu đã tắt, hoặc
  không thuộc `songId` khi gửi cả hai → 404 `MATERIAL_NOT_FOUND`.
- Tạo xong, mỗi ca viên được giao nhận một thông báo `AssignmentNotice`, `referenceType = "PracticeAssignment"`,
  `referenceId` = id bài tập.

### Ca viên nhận bài tập — `ChoirMember` (UC-09 / FE-10)

`PracticeAssignmentDetailDto`:

```json
{
  "id": "guid",
  "title": "Tập bài Nhập lễ",
  "instruction": "Thu âm bè Soprano, đoạn điệp khúc | null",
  "scope": "SkillGroup",
  "dueDate": "2026-10-12T15:00:00Z",
  "eventId": "guid | null",
  "eventDate": "2026-10-13 | null",
  "eventTime": "08:00:00 | null",
  "eventTitle": "string | null",
  "songId": "guid | null",
  "songTitle": "string | null",
  "materialId": "guid | null",
  "materialTitle": "string | null",
  "materialType": "SheetMusic | null",
  "latestSubmissionStatus": "Submitted | null",
  "latestSubmittedAt": "2026-10-10T09:00:00Z | null",
  "isOverdue": false
}
```

| Method | Path | Thành công | Lỗi |
|---|---|---|---|
| GET | `/api/practice-assignments/mine?isOpen=&status=&hasSubmission=&pageNumber=&pageSize=` | 200 `PagedList<PracticeAssignmentDetailDto>` | 404 `MEMBER_NOT_FOUND` |
| GET | `/api/practice-assignments/mine/counts` | 200 `MyPracticeAssignmentCountsResponse` | 404 `MEMBER_NOT_FOUND` |
| GET | `/api/practice-assignments/mine/{id}` | 200 `PracticeAssignmentDetailDto` | 404 `MEMBER_NOT_FOUND`, `PRACTICE_ASSIGNMENT_NOT_FOUND` |

- Ca viên thấy bài tập khi: scope `All`; có tên trong target; hoặc **hiện đang** được duyệt một skill của target.
  Skill được duyệt sau vẫn thấy bài giao trước đó; ca viên vào đoàn sau thấy cả bài `All` cũ.
- Bài không dành cho ca viên → 404 `PRACTICE_ASSIGNMENT_NOT_FOUND` (không trả 403).
- Bộ lọc (kết hợp kiểu AND, bỏ trống = không lọc). Sắp theo `dueDate` tăng dần.
  - `isOpen`: `true` = chưa tới hạn, `false` = đã qua hạn.
  - `status`: `Submitted` / `Passed` / `NeedsRevision` = lần nộp **mới nhất** của ca viên có trạng thái đó;
    `Overdue` = bài đang quá hạn (cùng luật với `isOverdue`).
  - `hasSubmission`: `false` = chưa nộp lần nào, `true` = đã nộp ít nhất một lần.
  - Ví dụ tab: "Cần làm" `hasSubmission=false&isOpen=true` · "Cần nộp lại" `status=NeedsRevision` ·
    "Chờ chấm" `status=Submitted` · "Quá hạn" `status=Overdue`.
- `latestSubmissionStatus` / `latestSubmittedAt` là lần nộp mới nhất **của chính ca viên**; `null` = chưa nộp.
- `isOverdue` (UC-10 / FE-12): đã qua `dueDate` mà chưa có lần nộp `Passed` — chưa nộp, hoặc lần mới nhất còn
  `Submitted` / `NeedsRevision`. Server tính lúc đọc; FE hiển thị "Quá hạn" theo field này, không tự tính.
  Không bản thu nào mang `status = Overdue` (nộp sau hạn bị chặn).
- Mở file tư liệu qua `api/music-materials` bằng `materialId`.

`MyPracticeAssignmentCountsResponse` (`GET mine/counts`) — số bài tập ca viên đang nhận, theo lần nộp mới nhất:

```json
{ "total": 12, "notSubmitted": 3, "submitted": 2, "needsRevision": 1, "passed": 6, "overdue": 2 }
```

- `notSubmitted + submitted + needsRevision + passed = total` — mỗi bài đúng một nhóm.
- `overdue` **chồng lên** các nhóm trên (bài quá hạn vẫn nằm trong `notSubmitted` / `submitted` / `needsRevision`),
  khớp với `status=Overdue` của danh sách.

### Ca viên nộp bản thu — `ChoirMember` (UC-09 / FE-11)

`PracticeSubmissionDto`:

```json
{
  "id": "guid",
  "practiceAssignmentId": "guid",
  "attemptNo": 2,
  "submittedAt": "2026-10-10T09:00:00Z",
  "status": "Submitted",
  "durationSeconds": 95,
  "audioUrl": "https://...signed..."
}
```

| Method | Path | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/practice-assignments/{id}/submissions` | `multipart/form-data`: `file`, `durationSeconds?` | 200 `PracticeSubmissionDto` | 400 `VALIDATION_FAILED`, `PRACTICE_AUDIO_REQUIRED`, `PRACTICE_AUDIO_TYPE_NOT_ALLOWED` · 404 `MEMBER_NOT_FOUND`, `PRACTICE_ASSIGNMENT_NOT_FOUND` · 409 `PRACTICE_SUBMISSION_ALREADY_PASSED`, `PRACTICE_SUBMISSION_PAST_DUE`, `PRACTICE_SUBMISSION_CONFLICT` · 413 `PRACTICE_AUDIO_TOO_LARGE` · 502 `EXTERNAL_STORAGE_FAILED` |

- `file`: `.mp3`, `.m4a`, `.wav`, tối đa 20 MB. Server kiểm đuôi và dung lượng, không tin `Content-Type`.
- `durationSeconds`: do app ghi âm báo, 1–3600, chỉ để hiển thị.
- Bài không dành cho ca viên → 404 `PRACTICE_ASSIGNMENT_NOT_FOUND`.
- Nộp được nhiều lần tới hạn nộp; mỗi lần là một bản thu mới, `attemptNo` tăng dần từ 1, `status = Submitted`.
  Lần mới nhất đã `Passed` → 409 `PRACTICE_SUBMISSION_ALREADY_PASSED`. Quá `dueDate` → 409 `PRACTICE_SUBMISSION_PAST_DUE`.
- Hai lần nộp cùng lúc (bấm hai lần) → lần sau nhận 409 `PRACTICE_SUBMISSION_CONFLICT`; tải lại bài tập rồi nộp lại.
- `audioUrl` là URL ký, hết hạn sau ít phút; cần nghe lại thì gọi lại API. Không gửi thông báo cho ca trưởng.

---

## 7d. Practice submissions — `api/practice-submissions` · nghe / chấm / nhận xét: `ChoirDirector` (UC-29) · bản thu của tôi: `ChoirMember` (UC-10)

`PracticeSubmissionDetailDto`: đủ các field của `PracticeSubmissionDto`, thêm người nộp, bài tập và các lần nhận xét:

```json
{
  "...": "các field của PracticeSubmissionDto",
  "memberId": "guid",
  "memberName": "Nguyễn Văn An",
  "memberAvatarUrl": "string | null",
  "assignmentTitle": "Tập bài Nhập lễ",
  "assignmentDueDate": "2026-10-12T15:00:00Z",
  "feedbacks": [ /* PracticeFeedbackDto, cũ nhất trước */ ]
}
```

`PracticeFeedbackDto` — mỗi lần chấm / nhận xét là một dòng; dòng **cuối** mang kết quả hiện tại (trùng `status`):

```json
{
  "id": "guid",
  "result": "NeedsRevision",
  "comment": "Giữ nốt cuối dài hơn | null",
  "reviewerId": "guid",
  "reviewerName": "string | null",
  "reviewedAt": "2026-10-10T10:00:00Z"
}
```

| Method | Path | Thành công | Lỗi |
|---|---|---|---|
| GET | `/api/practice-assignments/{id}/submissions?status=&allAttempts=&pageNumber=&pageSize=` | 200 `PagedList<PracticeSubmissionDetailDto>` | 404 `PRACTICE_ASSIGNMENT_NOT_FOUND` |
| GET | `/api/practice-submissions?status=&allAttempts=&pageNumber=&pageSize=` | 200 `PagedList<PracticeSubmissionDetailDto>` | — |
| GET | `/api/practice-submissions/{id}` | 200 `PracticeSubmissionDetailDto` | 404 `PRACTICE_SUBMISSION_NOT_FOUND` |

- `allAttempts`: mặc định `false` = mỗi ca viên chỉ lần nộp **mới nhất** của mỗi bài; `true` = mọi lần nộp.
- `status` (`Submitted` | `Passed` | `NeedsRevision` | `Overdue`) lọc **sau** bước trên: hàng chờ chấm là
  `GET /api/practice-submissions?status=Submitted` — chỉ ca viên có lần mới nhất còn chờ chấm.
- Thứ tự: theo một bài tập → tên ca viên, lần mới nhất trước; hàng chờ (mọi bài tập) → `submittedAt` cũ nhất trước.
- `audioUrl` hết hạn sau ít phút: gọi `GET /api/practice-submissions/{id}` ngay trước khi phát để lấy URL mới.

### Ca trưởng chấm bản thu (UC-29 / FE-43, FE-44)

`PracticeSubmissionReviewDto`: đủ các field của `PracticeSubmissionDetailDto` (`status` đã là kết quả mới), thêm:

```json
{
  "...": "các field của PracticeSubmissionDetailDto",
  "feedback": { /* PracticeFeedbackDto vừa tạo — cũng là phần tử cuối của feedbacks */ }
}
```

| Method | Path | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/practice-submissions/{id}/feedback` | `{ "result", "comment?" }` | 200 `PracticeSubmissionReviewDto` | 400 `VALIDATION_FAILED` · 404 `PRACTICE_SUBMISSION_NOT_FOUND` · 409 `PRACTICE_SUBMISSION_SUPERSEDED`, `PRACTICE_SUBMISSION_ALREADY_REVIEWED` |

- `result`: chỉ `Passed` hoặc `NeedsRevision`. `comment` tối đa 1000 ký tự, **bắt buộc khi `NeedsRevision`**.
- Chỉ chấm được **lần nộp mới nhất** của ca viên cho bài đó; lần cũ → 409 `PRACTICE_SUBMISSION_SUPERSEDED`.
- Mỗi bản thu chấm **một lần**: `status` khác `Submitted` → 409 `PRACTICE_SUBMISSION_ALREADY_REVIEWED`. Hai ca trưởng
  chấm cùng lúc thì người sau cũng nhận mã này, kết quả của người trước được giữ. Đổi kết quả sau đó: dùng `/comments`.
- Quá hạn nộp vẫn chấm được.
- Chấm xong, ca viên nhận thông báo `PracticeFeedback`, `referenceType = "PracticeSubmission"`, `referenceId` = id bản thu.
  Nội dung thông báo không chứa nhận xét; ca viên mở bản thu để đọc.
- `NeedsRevision` → ca viên nộp lại được (lần nộp mới). `Passed` → không nộp thêm cho bài đó.

### Ca trưởng nhận xét thêm / đổi kết quả (UC-29 / FE-44)

| Method | Path | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/practice-submissions/{id}/comments` | `{ "comment", "result?" }` | 200 `PracticeSubmissionDetailDto` | 400 `VALIDATION_FAILED` · 404 `PRACTICE_SUBMISSION_NOT_FOUND` · 409 `PRACTICE_SUBMISSION_NOT_REVIEWED`, `PRACTICE_SUBMISSION_SUPERSEDED`, `PRACTICE_SUBMISSION_ALREADY_REVIEWED` |

- Chỉ cho bản thu **đã chấm** (`Passed` / `NeedsRevision`); còn `Submitted` → 409 `PRACTICE_SUBMISSION_NOT_REVIEWED`,
  chấm qua `/feedback` trước.
- `comment` **bắt buộc**, tối đa 1000 ký tự. Mỗi lần gọi thêm **một dòng** vào `feedbacks`, không sửa dòng cũ.
- `result` bỏ trống (hoặc bằng kết quả hiện tại) = chỉ nhận xét thêm; được trên **mọi** lần nộp đã chấm.
- `result` khác kết quả hiện tại = **đổi kết quả** (`Passed` ↔ `NeedsRevision`); chỉ trên **lần nộp mới nhất**,
  lần cũ → 409 `PRACTICE_SUBMISSION_SUPERSEDED`. Hai ca trưởng đổi cùng lúc → người sau 409 `PRACTICE_SUBMISSION_ALREADY_REVIEWED`.
- Đổi `Passed` → `NeedsRevision` mở lại việc nộp, nhưng nếu đã quá `dueDate` thì ca viên vẫn không nộp được
  (chưa có chức năng gia hạn bài tập).
- Ca viên nhận thông báo `PracticeFeedback` ("New practice feedback" hoặc "Practice result changed"),
  `referenceId` = id bản thu.

### Ca viên xem bản thu & nhận xét (UC-10 / FE-12, FE-13)

| Method | Path | Thành công | Lỗi |
|---|---|---|---|
| GET | `/api/practice-submissions/mine?assignmentId=&pageNumber=&pageSize=` | 200 `PagedList<PracticeSubmissionDetailDto>` | 404 `MEMBER_NOT_FOUND` |
| GET | `/api/practice-submissions/mine/{id}` | 200 `PracticeSubmissionDetailDto` | 404 `MEMBER_NOT_FOUND`, `PRACTICE_SUBMISSION_NOT_FOUND` |

- Chỉ bản thu **của chính ca viên**, mọi lần nộp, `submittedAt` mới nhất trước; `assignmentId` lọc theo một bài tập.
- Bản thu của người khác → 404 `PRACTICE_SUBMISSION_NOT_FOUND` (không trả 403).
- Mở từ thông báo `PracticeFeedback`: gọi `GET /api/practice-submissions/mine/{referenceId}`.
- Trạng thái "Quá hạn" của **bài tập** xem ở `isOverdue` của `GET /api/practice-assignments/mine`.

---

## 7e. Rehearsals — `api/rehearsals` · role `ChoirDirector` (UC-30 / FE-45)

### `RehearsalAttendanceDto`

```json
{ "memberId": "guid", "fullName": "Nguyễn Văn A", "status": "Present", "checkedAt": "2026-10-09T12:00:00Z" }
```

`status`: `Present` | `Absent` | `Late` | `Excused`, hoặc `null` khi chưa điểm danh (khi đó `checkedAt` cũng `null`).

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/rehearsals/{id}/attendances` | — | 200 `RehearsalAttendanceDto[]` | 404 `REHEARSAL_NOT_FOUND` |
| PUT | `/api/rehearsals/{id}/attendances` | `{ "items": [{ "memberId", "status" }] }` | 204 | 400 `VALIDATION_FAILED` (`ATTENDANCE_MEMBER_DUPLICATE`) · 404 `REHEARSAL_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `REHEARSAL_NOT_STARTED`, `MEMBER_NOT_ACTIVE`, `ATTENDANCE_ALREADY_RECORDED` |

- Buổi chuẩn bị cho sự kiện cũng là một buổi tập (`Rehearsal` có gắn sự kiện), điểm danh giống hệt.
- `GET` trả mọi ca viên đang hoạt động, cộng những người đã được điểm danh ở buổi này nhưng nay không còn
  hoạt động; sắp theo tên. Không phân trang.
- `PUT` ghi hoặc sửa điểm danh cho từng ca viên trong `items`; ca viên không có trong `items` giữ nguyên.
  Ghi lại `checkedAt` = lúc gọi và người điểm danh là ca trưởng đang gọi.
- Chỉ điểm danh được từ giờ bắt đầu buổi tập trở đi (sửa sau khi kết thúc vẫn được); trước đó → 409 `REHEARSAL_NOT_STARTED`.
- Ca viên trong `items` phải đang hoạt động; mọi lỗi đều chặn cả lô, không ghi dòng nào.
- 409 `ATTENDANCE_ALREADY_RECORDED`: ca trưởng khác vừa điểm danh cùng ca viên — tải lại danh sách rồi gửi lại.

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

## 9. Lookups — `api/lookups` · đọc: mọi role đã đăng nhập · ghi: `Admin` (UC-32 / FE-49, FE-50)

Danh mục cho dropdown. Chỉ trả dòng **đang hoạt động**; Admin tắt một dòng thì nó biến khỏi
danh sách (dữ liệu cũ vẫn trỏ tới nó). Không phân trang — trả mảng.

| Route | Phần tử | Sắp theo |
|---|---|---|
| `GET /api/lookups/mass-types` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/ceremony-types` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/event-categories` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/song-themes` | `{ "id", "name", "description" }` | tên |
| `GET /api/lookups/skill-categories` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/liturgical-seasons` | `{ "id", "name", "startDate", "endDate", "colorHex", "isActive" }` | `startDate` |
| `GET /api/lookups/liturgical-slots` | `{ "id", "name", "defaultOrder" }` | `defaultOrder` (thứ tự trong lễ) |
| `GET /api/lookups/worship-locations` | `{ "id", "name", "address" }` | tên |
| `GET /api/lookups/skills?categoryId=` | `{ "id", "categoryId", "name", "description", "isActive" }` | tên |

- `description`, `address`, `colorHex` có thể `null`.
- `skills`: `categoryId` tuỳ chọn; bỏ qua kỹ năng thuộc nhóm đã tắt.
- `?includeInactive=true` trên `mass-types`, `ceremony-types`, `event-categories`, `skill-categories`,
  `liturgical-seasons`, `skills`: trả cả dòng đã tắt (kể cả kỹ năng thuộc nhóm đã tắt) — **chỉ Admin**; role khác
  gửi cờ này bị bỏ qua.

### Admin cấu hình danh mục (UC-32 / FE-49, FE-50)

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST · PUT `{id}` | `/api/lookups/mass-types` · `ceremony-types` · `event-categories` · `skill-categories` | `{ "name", "description?", "isActive" }` | 200 phần tử như bảng trên | 400 `VALIDATION_FAILED` · 404 `LOOKUP_NOT_FOUND` (PUT) · 409 `LOOKUP_NAME_DUPLICATE` |
| POST · PUT `{id}` | `/api/lookups/skills` | `{ "categoryId", "name", "description?", "isActive" }` | 200 `SkillDto` | 400 `VALIDATION_FAILED` · 404 `SKILL_CATEGORY_NOT_FOUND`, `SKILL_NOT_FOUND` (PUT) · 409 `SKILL_NAME_DUPLICATE` |
| POST · PUT `{id}` | `/api/lookups/liturgical-seasons` | `{ "name", "startDate", "endDate", "colorHex?", "isActive" }` | 200 `LiturgicalSeasonDto` | 400 `VALIDATION_FAILED` (`SEASON_DATE_INVALID`) · 404 `LOOKUP_NOT_FOUND` (PUT) · 409 `SEASON_DATE_OVERLAP` |

- `isActive` mặc định `true`. **Không có xoá**: các bảng khác đang trỏ tới danh mục, nên muốn bỏ thì gửi
  `PUT` với `isActive: false`. Bật lại bằng `isActive: true`.
- `PUT` gửi đủ mọi trường (thay toàn bộ). `name` được cắt khoảng trắng hai đầu.
- `name`: bắt buộc, tối đa 50 ký tự (mùa phụng vụ: 100); `description` tối đa 300; trùng tên (không phân biệt
  hoa thường) với dòng khác → 409. Tên kỹ năng chỉ cần duy nhất **trong nhóm**.
- Mùa phụng vụ: `endDate` phải sau `startDate`, nếu không → `SEASON_DATE_INVALID`; `colorHex` dạng `#RRGGBB`.
  Tên mùa được trùng (mỗi năm một dòng), nhưng một mùa đang hoạt động không được trùng khoảng ngày với mùa
  đang hoạt động khác → 409 `SEASON_DATE_OVERLAP`.
- Id của 5 nhóm kỹ năng là cố định trên mọi môi trường:

| Nhóm | Id |
|---|---|
| Vocal | `c4829abd-bb1d-4c6c-b401-9a177a88e66a` |
| Instrument | `0904ad8b-87f2-46c5-9938-f6cfbf0fa70b` |
| Solo | `550a67bb-0393-4bc4-8fe0-d7a453871f81` |
| Psalm | `a1c322c5-14af-4ca4-892a-b6e8d0eacbbd` |
| Conducting support | `e2722720-3745-48ea-9e8e-14ec7a63907d` |

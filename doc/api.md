# Harmonia API

Tài liệu cho FE (web) và mobile. Chỉ liệt kê endpoint **đã có trong code**; thêm controller
mới thì cập nhật file này trong cùng PR.

- Base URL dev: `http://localhost:5259` · `https://localhost:7112`
- Swagger (dev): `/swagger` — mọi endpoint có schema của response thành công (200 kèm kiểu body, hoặc 204);
  response lỗi không khai trong Swagger vì luôn cùng một dạng (mục 1).
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
| Hai request ghi đè nhau (bấm hai lần, hai người sửa cùng lúc) | 409 | `CONFLICT` → tải lại dữ liệu rồi thử lại |
| Lỗi không lường trước | 500 | `INTERNAL_ERROR` |

### Phân trang

Endpoint danh sách nhận query `pageNumber` (mặc định 1) và `pageSize` (mặc định 20, tối đa 100;
giá trị ngoài khoảng tự kẹp lại, không báo lỗi).

Bộ lọc trên query thì **có** kiểm: `keyword` dài quá 100 ký tự, id lọc là guid rỗng
(`00000000-…`), hoặc giá trị enum ngoài danh sách → 400 `VALIDATION_FAILED`. Bỏ trống bộ lọc thì hợp lệ.
`fromDate` sau `toDate` → 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`). Trả về:

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

## 4. Member profiles — `api/member-profiles` · của tôi: `ChoirMember` · quản lý: `ChoirDirector` · tìm kiếm: `ChoirDirector`, `Admin`

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
| GET | `/api/member-profiles` | `ChoirDirector`, `Admin` | query `keyword`, `status`, `skillId`, `pageNumber`, `pageSize` | 200 `PagedList<MemberProfileSummaryDto>` | — |
| GET | `/api/member-profiles/{id}` | `ChoirDirector` | — | 200 `MemberProfileDto` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/member-profiles/{id}` | `ChoirDirector` | `{ "phone", "dateOfBirth", "joinedDate", "status" }` | 200 `MemberProfileDto` | 400 `VALIDATION_FAILED`, `MEMBER_JOINED_DATE_IN_FUTURE` · 404 `MEMBER_NOT_FOUND` |
| GET | `/api/member-profiles/me/history` | `ChoirMember` | query `liturgicalSeasonId`, `fromDate`, `toDate`, `pageNumber`, `pageSize` | 200 `PagedList<ParticipationHistoryDto>` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) · 404 `MEMBER_NOT_FOUND` |

- `PUT` thay **toàn bộ** các field trong body: field bỏ trống / `null` sẽ bị xoá giá trị
  (`phone`, `dateOfBirth`). Gửi lại giá trị cũ nếu không muốn đổi.
- `PUT .../me`: `fullName` bắt buộc (≤ 100 ký tự), đổi tên hiển thị của chính tài khoản đó.
  `phone` ≤ 20 ký tự. `dateOfBirth` không được ở tương lai.
- `PUT .../{id}`: `joinedDate` bắt buộc, không ở tương lai. Ca trưởng **không** sửa tên ca viên
  (tên do ca viên tự sửa, hoặc Admin sửa qua `PUT /api/users/{id}`).
- `GET /api/member-profiles`: `keyword` khớp một phần tên hoặc email; `skillId` chỉ giữ ca viên đã
  được **duyệt** kỹ năng đó (lấy id từ `GET /api/lookups/skills`; skill đã tắt → danh sách rỗng); các bộ lọc kết hợp AND; sắp theo tên.
  Dùng để chọn người khi phân công theo bè / nhạc cụ. Trả ca viên **toàn giáo xứ** (D8), để ca trưởng tìm được
  người thuộc ca đoàn khác mà thêm vào ca đoàn mình.
- `GET/PUT .../{id}` chỉ làm được với ca viên ca trưởng **quản lý** (D8): đang ở một ca đoàn có `directorId` là
  ca trưởng đó, hoặc chưa ở ca đoàn nào. Ca viên khác → 404 `MEMBER_NOT_FOUND`. Ca viên ở ca đoàn của hai
  ca trưởng thì cả hai cùng sửa được, kể cả `status`.
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

- `pending` trả bản khai `Pending` của ca viên ca trưởng **quản lý** (D8 — cùng luật với `GET /api/member-profiles/{id}`),
  cũ nhất trước. Duyệt / từ chối bản khai của ca viên khác → 404 `MEMBER_SKILL_NOT_FOUND`.
- `reason` bắt buộc, tối đa 500 ký tự.
- Chỉ duyệt/từ chối được bản `Pending`; bản đã duyệt hoặc đã từ chối → 409.
- Duyệt hoặc từ chối xong, ca viên nhận notification `type = SkillReview`,
  `referenceType = "MemberSkill"`, `referenceId` = id bản khai (S-05).

---

## 4b. Lịch phụng vụ & sự kiện — `api/liturgical-days`, `api/liturgical-events` · role `ParishPriest` · danh sách sự kiện, tiến độ chuẩn bị: `ChoirDirector` · tra ngày phụng vụ, lịch sắp tới `api/schedule`: mọi role (UC-12 / UC-04 / UC-15 / UC-30)

### `LiturgicalDayDto`

```json
{
  "date": "2026-12-25", "celebrationName": "Christmas", "rank": "Solemnity", "seasonName": "Christmas",
  "celebrationNameVi": "Chúa Giáng Sinh", "rankVi": "Lễ trọng", "seasonNameVi": "Mùa Giáng Sinh"
}
```

- `celebrationName`, `rank`, `seasonName` giữ nguyên tiếng Anh của lịch (dùng làm mã). FE hiển thị các trường `…Vi`.
- `…Vi` lấy theo thứ tự: bản dịch Admin quản lý (mục 4h) → quy tắc mẫu cho tên lễ (vd. "29th Sunday of Ordinary Time"
  → "Chúa nhật XXIX Thường niên") → tiếng Anh nguyên văn khi chưa có bản dịch. Dịch lúc đọc nên Admin sửa là thấy ngay,
  không cần đồng bộ lại năm.

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
`RehearsalSummaryDto` = `{ id, startTime, endTime, locationName, note, songs }`.

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/liturgical-days?fromDate=&toDate=` · mọi role | — | 200 `LiturgicalDayDto[]` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`, `REPORT_DATE_RANGE_TOO_LARGE`) |
| GET | `/api/liturgical-days/{date}` (`date` dạng `yyyy-MM-dd`) · mọi role | — | 200 `LiturgicalDayDto` | 404 `CALENDAR_DAY_NOT_FOUND` |
| POST | `/api/liturgical-days/sync` | `{ "year": 2027 }` | 200 số ngày đã lưu (`int`) | 400 `VALIDATION_FAILED` · 502 `EXTERNAL_CALENDAR_FAILED` |
| GET | `/api/liturgical-events?fromDate=&toDate=&status=&pageNumber=&pageSize=` · `ParishPriest`, `ChoirDirector` | — | 200 `PagedList<LiturgicalEventListItemDto>` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) |
| GET | `/api/liturgical-events/{id}` | — | 200 `LiturgicalEventDto` | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| POST | `/api/liturgical-events` | `{ "eventDate", "time", "liturgicalSeasonId?", "massTypeId?", "ceremonyTypeId?", "categoryId?", "locationId", "title?", "specialRequirements?" }` | 200 `LiturgicalEventDto` | 400 `VALIDATION_FAILED` (`LITURGICAL_EVENT_TYPE_REQUIRED`), `LITURGICAL_EVENT_DATE_OUTSIDE_LITURGICAL_SEASON` · 404 `LOOKUP_NOT_FOUND` · 409 `LITURGICAL_EVENT_SLOT_TAKEN`, `LOOKUP_INACTIVE` |
| PUT | `/api/liturgical-events/{id}` | như `POST` | 200 `LiturgicalEventDto` | 400 `VALIDATION_FAILED` (`LITURGICAL_EVENT_TYPE_REQUIRED`), `LITURGICAL_EVENT_DATE_OUTSIDE_LITURGICAL_SEASON` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `LOOKUP_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_SLOT_TAKEN`, `LOOKUP_INACTIVE` |
| POST | `/api/liturgical-events/generation` | `{ "fromDate", "toDate" }` | 200 `LiturgicalEventDto[]` (các sự kiện vừa tạo) | 400 `VALIDATION_FAILED` (`LITURGICAL_EVENT_GENERATION_RANGE_INVALID`) |
| PATCH | `/api/liturgical-events/{id}/publish` | — | 200 `LiturgicalEventDto` | 400 `LITURGICAL_EVENT_TYPE_REQUIRED`, `LITURGICAL_EVENT_SEASON_REQUIRED`, `LITURGICAL_EVENT_DATE_OUTSIDE_LITURGICAL_SEASON` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `LOOKUP_NOT_FOUND` · 409 `LITURGICAL_EVENT_ALREADY_PUBLISHED`, `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `LITURGICAL_EVENT_SLOT_TAKEN`, `LOOKUP_INACTIVE` |
| PATCH | `/api/liturgical-events/{id}/cancel` | — | 200 `LiturgicalEventDto` | 404 `LITURGICAL_EVENT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED` |
| GET | `/api/schedule/events` · mọi role | — | 200 `LiturgicalEventSummaryDto[]` | — |
| GET | `/api/schedule/events/{id}` · mọi role | — | 200 `MemberEventDetailDto` | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| GET | `/api/schedule/rehearsals` · mọi role | query `fromDate?`, `toDate?` | 200 `RehearsalSummaryDto[]` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`, `REPORT_DATE_RANGE_TOO_LARGE`) |
| GET | `/api/schedule/rehearsals/{id}` · mọi role | — | 200 `MemberRehearsalDetailDto` | 404 `REHEARSAL_NOT_FOUND` |
| GET | `/api/schedule/assignments` · `ChoirMember` | — | 200 `MyAssignedEventDto[]` | 404 `MEMBER_NOT_FOUND` |
| GET | `/api/liturgical-events/{id}/preparation-progress` · `ChoirDirector` | — | 200 `EventPreparationProgressDto[]` | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| GET | `/api/liturgical-events/{id}/preparation-status` | — | 200 `EventPreparationStatusDto` | 404 `LITURGICAL_EVENT_NOT_FOUND` |

**Ngày phụng vụ (UC-12 / FE-15)**

- `GET ?fromDate=&toDate=`: cả hai bắt buộc, tính cả hai đầu, tối đa 366 ngày. Trả các ngày đã đồng bộ trong khoảng,
  sắp theo ngày; ngày chưa đồng bộ thì **không có trong mảng** (không báo lỗi). Dùng để hiện tên ngày trên lịch tháng.

- Ngày phụng vụ là bản cache của [Liturgical Calendar API](https://litcal.johnromanodorazio.com/) (lịch Rôma chung).
  `POST /sync` với `year` (năm dương lịch, 1970–9999) gọi API rồi **ghi đè** mọi ngày đã cache của năm đó; kết quả là
  số ngày đã lưu (365 / 366). API lỗi hoặc quá 30 giây → 502 `EXTERNAL_CALENDAR_FAILED`, cache giữ nguyên.
- API **không có tiếng Việt**: `celebrationName` là tên tiếng Anh. Hiển Linh, Thăng Thiên, Mình Máu Thánh Chúa đã dời
  sang Chúa nhật như ở Việt Nam. Mỗi ngày chỉ giữ lễ có bậc cao nhất, bỏ thánh lễ vọng.
- `rank`: `Solemnity` | `Feast` | `Obligatory Memorial` | `Optional Memorial`, `null` với ngày thường.
  `seasonName`: `Advent` | `Christmas` | `Lent` | `Easter` | `OrdinaryTime` (Tam Nhật Vượt Qua tính vào `Lent`).

**Sự kiện (UC-12 / FE-16)**

- Phải có `massTypeId` **hoặc** `ceremonyTypeId`, thiếu cả hai → 400 `VALIDATION_FAILED` với
  `errors` chứa `LITURGICAL_EVENT_TYPE_REQUIRED`. `title` tối đa 200, `specialRequirements` tối đa 1000 ký tự.
- Một ngày có nhiều sự kiện được, nhưng trùng cả ngày + giờ + địa điểm → 409 `LITURGICAL_EVENT_SLOT_TAKEN`.
  Sự kiện đã huỷ không giữ chỗ: tạo lại sự kiện mới đúng ngày + giờ + địa điểm đó được.
- Sự kiện mới luôn ở `Draft`. Công bố (`publish`) chuyển sang `Published`, ghi `publishedAt`, rồi gửi
  notification `type = EventPublished` cho mọi ca trưởng và ca viên đang hoạt động,
  `referenceType = "LiturgicalEvent"`, `referenceId` = id sự kiện (S-05).
- Mỗi sự kiện công bố riêng (D6). Ca viên chỉ thấy sự kiện `Published`.
- **Kiểm trước khi công bố** — sự kiện phải đủ và còn hợp lệ, kiểm theo thứ tự, gặp lỗi đầu tiên thì dừng:
  chưa qua ngày (409 `LITURGICAL_EVENT_ALREADY_PASSED`) · có `massTypeId` hoặc `ceremonyTypeId` (400
  `LITURGICAL_EVENT_TYPE_REQUIRED`) · có `liturgicalSeasonId` (400 `LITURGICAL_EVENT_SEASON_REQUIRED`) · mọi danh mục
  còn hoạt động (409 `LOOKUP_INACTIVE` — **kể cả** mục bị tắt sau lần lưu cuối, khác với `PUT`) và ngày nằm trong mùa ·
  ngày + giờ + địa điểm chưa bị sự kiện khác chiếm (409 `LITURGICAL_EVENT_SLOT_TAKEN`). Hai lỗi 400 ở đây trả thẳng mã,
  không bọc trong `VALIDATION_FAILED`. Sửa bằng `PUT` rồi công bố lại.
- **Sinh sự kiện từ giờ lễ cố định** (`POST /generation`, `ParishPriest`): `fromDate`, `toDate` tính cả hai đầu,
  `fromDate` không trước hôm nay (giờ Việt Nam), dài tối đa 62 ngày. Với mỗi ngày trong khoảng, mỗi giờ lễ cố định
  (mục 4g) đang bật có `dayOfWeek` trùng thứ của ngày → tạo một sự kiện `Draft` với `time`, `massTypeId`, `locationId`,
  `title` của giờ lễ đó; `liturgicalSeasonId` = mùa đang hoạt động chứa ngày đó (không có thì `null` — phải chọn mùa trước
  khi công bố). Bỏ qua (không báo lỗi): giờ lễ có loại lễ hoặc địa điểm đã tắt; ngày + giờ + địa điểm đã có sự kiện
  chưa huỷ — nên gọi lại cùng khoảng không tạo trùng. Trả mảng sự kiện vừa tạo, sắp theo ngày rồi giờ; `[]` khi không
  có gì để tạo. Không gửi thông báo.
- Danh sách (`GET`) trả sự kiện **mọi trạng thái** (kể cả `Draft`, `Cancelled`), sắp theo ngày rồi giờ; các bộ lọc
  `fromDate`, `toDate` (bao gồm hai đầu, `yyyy-MM-dd`) và `status` kết hợp AND, đều tuỳ chọn.
- **Tự điền mùa (`POST`)**: không gửi `liturgicalSeasonId` thì lấy mùa đang hoạt động có `startDate`–`endDate` chứa
  `eventDate` (cùng luật với sinh sự kiện từ giờ lễ cố định); không có mùa nào thì để `null`. `PUT` không tự điền.
- **Kiểm danh mục (cả `POST` lẫn `PUT`)**: `liturgicalSeasonId`, `massTypeId`, `ceremonyTypeId`, `categoryId`, `locationId`
  phải trỏ tới mục có thật → nếu không 404 `LOOKUP_NOT_FOUND`; mục đã tắt (`isActive: false`) → 409 `LOOKUP_INACTIVE`.
  Lỗi không cho biết trường nào sai. Khi `PUT`, Id giữ nguyên so với sự kiện hiện tại vẫn được chấp nhận dù mục đã tắt.
- **Ngày phải thuộc mùa**: có `liturgicalSeasonId` mà `eventDate` nằm ngoài `startDate − 1 ngày`–`endDate` của mùa → 400
  `LITURGICAL_EVENT_DATE_OUTSIDE_LITURGICAL_SEASON`. Cho lùi 1 ngày vì lễ vọng chiều tối thuộc mùa của ngày hôm sau:
  mùa nhập đúng lịch (Mùa Vọng đến 24/12, Giáng Sinh từ 25/12), lễ sáng 24/12 chọn Mùa Vọng, lễ vọng tối 24/12 chọn Giáng Sinh. Kiểm ở mọi lần lưu, nên sự kiện cũ đang lệch mùa phải chọn lại mùa (hoặc bỏ trống) mới sửa được.
- Sửa (`PUT`) được khi sự kiện `Draft` hoặc `Published`; gửi đủ mọi trường như `POST`. Sửa không đổi trạng thái,
  không gửi thông báo. Trùng ngày + giờ + địa điểm với sự kiện **khác** → 409 `LITURGICAL_EVENT_SLOT_TAKEN`.
- Huỷ (`cancel`) chuyển sang `Cancelled`, không hoàn tác được; sự kiện đã qua (trước hôm nay theo giờ Việt Nam)
  → 409 `LITURGICAL_EVENT_ALREADY_PASSED`. Nếu sự kiện đang `Published`, gửi notification `type = EventCancelled` cho mọi ca
  trưởng và ca viên đang hoạt động (`referenceType = "LiturgicalEvent"`); huỷ `Draft` thì không gửi.
- Hiện tại `locationName` trong response của `POST`, `generation` và `publish` là `null` (server chưa nạp địa điểm);
  lấy tên địa điểm từ `GET /api/lookups/worship-locations` theo `locationId`.

**Danh sách sự kiện — `LiturgicalEventListItemDto`**

Gồm mọi trường của `LiturgicalEventDto`, thêm `liturgicalSeasonName`, `massTypeName`, `ceremonyTypeName` (tên danh mục,
`null` khi không chọn) và `latestSongListStatus` (trạng thái phiên bản danh sách bài hát mới nhất, `null` khi chưa có).

- Có cả sự kiện đã qua lẫn sắp tới; lọc bằng `fromDate` / `toDate` / `status`. Sắp theo ngày rồi giờ.
- `ChoirDirector` **không thấy sự kiện `Draft`** (lọc `status=Draft` → trang rỗng); `ParishPriest` thấy mọi trạng thái.

**Lịch sắp tới (UC-04 / FE-05)**

- `events`: sự kiện `Published` từ hôm nay trở đi, sắp theo ngày rồi giờ.
- `rehearsals`: không gửi ngày → buổi tập **chưa kết thúc** tại thời điểm gọi (cả buổi đang diễn ra). Gửi `fromDate` +
  `toDate` (ngày giáo xứ, tính cả hai đầu, tối đa 93 ngày) → buổi tập **bắt đầu** trong khoảng đó, cả quá khứ lẫn tương lai;
  gửi một trong hai hoặc `fromDate > toDate` → `REPORT_DATE_RANGE_INVALID`, quá 93 ngày → `REPORT_DATE_RANGE_TOO_LARGE`.
  Sắp theo giờ bắt đầu; cùng luật lọc ca đoàn (D8) ở cả hai cách. Chỉ buổi tập
  thường (không gắn sự kiện) hoặc gắn sự kiện `Published`. `locationName`, `note` có thể `null`.
  `songs`: `RehearsalSongDto[]` (xem mục 7e) theo thứ tự hiển thị, `[]` khi ca trưởng chưa chọn bài.
- `events/{id}` — `MemberEventDetailDto` = `{ id, eventDate, time, title, locationName, liturgicalSeasonName,
  massTypeName, ceremonyTypeName, specialRequirements, participation, assignments, rehearsals }`. Chỉ sự kiện `Published`;
  `Draft`, đã huỷ hoặc không tồn tại → 404 `LITURGICAL_EVENT_NOT_FOUND`. Các phần riêng chỉ chứa dữ liệu của **người gọi**:
  - `participation`: `MyParticipationDto` (xem mục 7f), `null` khi người gọi không được mời xác nhận.
  - `assignments`: `MyAssignmentDto[]` = `{ skillId, skillName, songListItemId, songTitle }`, `[]` cho tới khi bảng
    phân công đã chốt và người gọi đã được thông báo. `songListItemId`, `songTitle` `null` khi dòng phân công không gắn bài cụ thể.
  - `rehearsals`: `MemberRehearsalDetailDto[]` của sự kiện, sắp theo giờ bắt đầu.
  Role khác `ChoirMember` gọi được, nhưng `participation` luôn `null` và `assignments` luôn `[]`.
- `rehearsals/{id}` — `MemberRehearsalDetailDto` = `RehearsalSummaryDto` + `myAttendanceStatus` (`Present` | `Absent` |
  `Late` | `Excused`, `null` khi chưa được điểm danh hoặc người gọi không phải ca viên). Chỉ buổi tập thường hoặc gắn sự
  kiện `Published`, kể cả buổi đã qua; còn lại → 404 `REHEARSAL_NOT_FOUND`.
- `assignments` — `MyAssignedEventDto` = `{ eventId, eventDate, time, title, locationName, assignments: MyAssignmentDto[] }`:
  sự kiện `Published` từ hôm nay trở đi mà ca viên có dòng phân công còn hiệu lực trên bảng **đã chốt** và đã được
  thông báo, sắp theo ngày rồi giờ. Không có → `[]`.
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
- `rosterShortages`: giống `GET /api/service-rosters/shortages?eventId=`; `null` khi chưa có danh sách bài hát được duyệt và sự kiện cũng không có yêu cầu cả buổi lễ, `[]` khi đủ người.
- `attendanceExpected` = `rehearsalsHeld` × số ca viên đang hoạt động; `attendancePresent` = tổng lượt `Present` + `Late`.
- `practice*`: cộng dồn các cột `assignments*` của `preparation-progress` trên mọi ca viên đang hoạt động.

---

## 4c. Reports — `api/reports` · role `ParishPriest`, `Admin` (UC-16 / FE-22) · 4 báo cáo quản trị và xuất file: `Admin` (UC-34 / FE-52, UC-34E / FE-53) · 3 báo cáo theo ca viên và xuất chúng: thêm `ChoirDirector`

| Method | Route | Role | Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/reports/service-history` | `ParishPriest`, `Admin` | `eventId`, `liturgicalSeasonId`, `fromDate`, `toDate`, `pageNumber`, `pageSize` | 200 `PagedList<ServiceHistoryDto>` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) |
| GET | `/api/reports/song-usage` | `ParishPriest`, `Admin` | như trên | 200 `PagedList<SongUsageDto>` | như trên |
| GET | `/api/reports/user-activity` | `Admin` | `fromDate`, `toDate`, `pageNumber`, `pageSize` | 200 `PagedList<UserActivityDto>` | như trên |
| GET | `/api/reports/rehearsal-attendance` | `Admin`, `ChoirDirector` | như `service-history` + `choirId` (chỉ giữ ca viên đang thuộc ca đoàn) | 200 `PagedList<RehearsalAttendanceReportDto>` | như trên · 404 `CHOIR_NOT_FOUND` (ca trưởng lọc `choirId` không phải ca đoàn của mình) |
| GET | `/api/reports/participation` | `Admin`, `ChoirDirector` | như `rehearsal-attendance` | 200 `PagedList<ParticipationReportDto>` | như `rehearsal-attendance` |
| GET | `/api/reports/assignment-completion` | `Admin`, `ChoirDirector` | như `rehearsal-attendance` | 200 `PagedList<PracticeAssignmentCompletionDto>` | như `rehearsal-attendance` |
| GET | `/api/reports/export` | `Admin`, `ChoirDirector` | `type`, `format`, `eventId`, `liturgicalSeasonId`, `fromDate`, `toDate`, `choirId` | 200 file tải về | 400 `VALIDATION_FAILED` (`REPORT_TYPE_NOT_SUPPORTED`, `REPORT_DATE_RANGE_INVALID`) · 400 `REPORT_TYPE_NOT_SUPPORTED` khi `ChoirDirector` chọn `type` ngoài `RehearsalAttendance`, `Participation`, `AssignmentCompletion` · 404 `REPORT_NO_DATA`, `CHOIR_NOT_FOUND` · 502 `EXTERNAL_STORAGE_FAILED` |
| GET | `/api/reports/activity-trend` | `Admin`, `ChoirDirector` | `groupBy` (`Week` \| `Month` \| `Year`, mặc định `Month`), `fromDate`, `toDate` (bắt buộc, tối đa 3 năm), `choirId` | 200 `ActivityTrendDto[]`: mỗi kỳ một dòng, cũ trước, có cả kỳ trống | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) · 404 `CHOIR_NOT_FOUND` (như `rehearsal-attendance`) |
| GET | `/api/reports/columns/{type}` | `ParishPriest`, `Admin`, `ChoirDirector` | `type` = tên `ReportType` (vd. `RehearsalAttendance`) | 200 `ReportColumnDto[]` (`key`, `title`): cột theo thứ tự, tiêu đề tiếng Việt trùng với file xuất | 400 `REPORT_TYPE_NOT_SUPPORTED` |
| GET | `/api/reports/exports` | `Admin` | `pageNumber`, `pageSize` | 200 `PagedList<ReportExportDto>` | — |

- `eventId` thu hẹp báo cáo về một sự kiện; `user-activity` bỏ qua tham số này.
- Các bộ lọc đều tuỳ chọn, kết hợp AND; `fromDate`, `toDate` bao gồm hai đầu (`yyyy-MM-dd`, giờ Việt Nam).
  `fromDate` > `toDate` → 400, mã `REPORT_DATE_RANGE_INVALID` ở trường `toDate`.
- `service-history` và `song-usage` chỉ xét sự kiện `Published` **trước hôm nay** (giờ Việt Nam).
- Role khác gọi vào → `403` body rỗng.

### Thống kê theo kỳ — `activity-trend`

Một request cho cả năm thay vì cộng dồn từng buổi tập ở FE. Cùng nhóm ca viên với ba báo cáo theo ca viên
(ca trưởng: ca viên mình quản lý; `choirId` thu hẹp về một ca đoàn). Tuần bắt đầu thứ Hai; mọi ngày theo giờ Việt Nam;
kỳ đầu và kỳ cuối bị cắt theo `fromDate` / `toDate`.

```json
{
  "periodStart": "2026-08-01", "periodEnd": "2026-08-31",
  "rehearsalsRecorded": 4, "presentCount": 30, "lateCount": 3, "absentCount": 5, "excusedCount": 2,
  "attendanceRate": 0.825,
  "assignmentsDue": 2, "assignedCount": 20, "passedCount": 14, "needsRevisionCount": 2,
  "awaitingReviewCount": 1, "notSubmittedCount": 3, "completionRate": 0.7
}
```

- Điểm danh: buổi tập **bắt đầu** trong kỳ. `rehearsalsRecorded` = số buổi có ít nhất một lượt điểm danh.
  `attendanceRate` = (`present` + `late`) / tổng lượt điểm danh, `null` khi kỳ không có lượt nào.
- Luyện tập: bài tập **hạn nộp** trong kỳ, xét lần nộp mới nhất của từng ca viên được giao (như `assignment-completion`).
  `assignedCount` = số cặp ca viên–bài tập; `completionRate` = `passed` / `assigned`, `null` khi không có bài.

### Xuất báo cáo (UC-34E / FE-53)

- `type` (bắt buộc): `UserActivity`, `RehearsalAttendance`, `Participation`, `AssignmentCompletion`, `SongUsage`,
  `ServiceHistory`. Thiếu hoặc sai → 400, mã `REPORT_TYPE_NOT_SUPPORTED` ở trường `type`.
- `format`: `Csv` (mặc định), `Xlsx`, `Pdf`, `Xml`, `Json`. Giá trị lạ → 400 `VALIDATION_FAILED`.
- Bộ lọc giống báo cáo đang xem. "Theo tháng": gửi `fromDate` / `toDate` là ngày đầu và ngày cuối tháng;
  "theo sự kiện": `eventId`; "theo mùa phụng vụ": `liturgicalSeasonId`.
- File chứa **mọi dòng** khớp bộ lọc — `pageNumber`, `pageSize` bị bỏ qua. Báo cáo rỗng → 404 `REPORT_NO_DATA`.
- Response là file nhị phân, tên trong header `Content-Disposition` dạng `RehearsalAttendance-20261011-153000.csv`
  (giờ Việt Nam). `Content-Type` theo định dạng: `text/csv`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`,
  `application/pdf`, `text/xml`, `application/json`.
- CSV, XLSX, PDF có tiêu đề cột tiếng Việt và ngày dạng `dd/MM/yyyy`; XML, JSON dùng tên trường của API và ngày ISO.
  CSV là UTF-8 có BOM; ô bắt đầu bằng `=`, `+`, `-`, `@` được thêm `'` phía trước.
- Mỗi lần xuất được lưu lại: file cất riêng tư trên kho lưu trữ, kho lỗi thì cả lần xuất trả `EXTERNAL_STORAGE_FAILED`
  và không có file nào được phát ra.

#### `ReportExportDto` — lịch sử xuất

```json
{
  "id": "guid", "reportType": "RehearsalAttendance", "format": "Xlsx",
  "eventId": null, "liturgicalSeasonId": "guid", "fromDate": "2026-10-01", "toDate": "2026-10-31",
  "exportedBy": "guid", "exportedByName": "Quản trị viên", "exportedAt": "2026-10-11T08:30:00Z",
  "downloadUrl": "https://..."
}
```

- Mới nhất trước; mọi Admin thấy lịch sử của nhau. Bốn trường bộ lọc là giá trị đã dùng lúc xuất, có thể `null`.
- `downloadUrl` là link ký ngắn hạn tới đúng file đã xuất; hết hạn thì gọi lại danh sách để lấy link mới.

### Báo cáo quản trị (UC-34 / FE-52)

Ba báo cáo theo ca viên (`rehearsal-attendance`, `participation`, `assignment-completion`) liệt kê **mọi ca viên
`Active`**, sắp theo tên. Bộ lọc chỉ thu hẹp dữ liệu được đếm, không bỏ ca viên khỏi danh sách — ca viên không có
dữ liệu trong khoảng lọc vẫn hiện với các số bằng 0.

#### `UserActivityDto` — hoạt động tài khoản

```json
{
  "userId": "guid", "fullName": "Nguyễn Văn A", "email": "a@example.com", "roleName": "ChoirMember",
  "isActive": true, "lastLoginAt": "2026-10-10T12:30:00Z",
  "passwordChangeCount": 1, "passwordResetCount": 0, "accountChangeCount": 2, "actionCount": 5
}
```

- Mỗi dòng một tài khoản (kể cả đã khoá), sắp theo email. `liturgicalSeasonId` không áp dụng cho báo cáo này.
- `lastLoginAt`: giá trị hiện tại, không phụ thuộc khoảng lọc; `null` nếu chưa đăng nhập lần nào.
- `passwordChangeCount`: số lần tự đổi mật khẩu khi đang đăng nhập. `passwordResetCount`: số lần đặt lại qua link
  email. Hai số này chỉ có từ khi hệ thống bắt đầu ghi sự kiện đổi mật khẩu (2026-10-11).
- `accountChangeCount`: số lần tài khoản bị sửa — role, khoá / mở, họ tên, số điện thoại, email.
- `actionCount`: số dòng nhật ký hoạt động do người này gây ra (duyệt kỹ năng, duyệt danh sách bài hát, chốt phân công…).
- Hệ thống không lưu từng lần đăng nhập / đăng xuất, nên không có số đếm cho hai việc đó.

#### `RehearsalAttendanceReportDto` — điểm danh buổi tập

```json
{ "memberId": "guid", "fullName": "Nguyễn Văn A", "presentCount": 6, "lateCount": 1, "absentCount": 1, "excusedCount": 0 }
```

- Đếm các dòng điểm danh đã ghi ở buổi tập **bắt đầu** trong khoảng lọc. Buổi chưa điểm danh không tính vào số nào.
- Có `liturgicalSeasonId` thì chỉ tính buổi tập gắn với sự kiện thuộc mùa đó; buổi tập không gắn sự kiện bị bỏ.

#### `ParticipationReportDto` — xác nhận tham gia

```json
{ "memberId": "guid", "fullName": "Nguyễn Văn A", "invitedCount": 1, "confirmedCount": 7, "declinedCount": 1, "unsureCount": 0 }
```

- Đếm theo trạng thái xác nhận ở các sự kiện `Published` có ngày trong khoảng lọc, **gồm cả sự kiện sắp tới**.
  `invitedCount` là số yêu cầu chưa trả lời.

#### `PracticeAssignmentCompletionDto` — hoàn thành bài tập

```json
{
  "memberId": "guid", "fullName": "Nguyễn Văn A",
  "assignedCount": 5, "passedCount": 3, "needsRevisionCount": 1, "awaitingReviewCount": 0, "notSubmittedCount": 1
}
```

- Xét bài tập có **hạn nộp** trong khoảng lọc mà ca viên nhận (giao cho tất cả, giao đích danh, hoặc giao theo
  kỹ năng ca viên đang được duyệt). Có `liturgicalSeasonId` thì chỉ tính bài gắn với sự kiện thuộc mùa đó.
- Mỗi bài rơi vào đúng một trong bốn số sau theo **lần nộp mới nhất**; tổng bốn số bằng `assignedCount`.
  `awaitingReviewCount`: đã nộp, chưa chấm. `notSubmittedCount`: chưa nộp.

### `ServiceHistoryDto` — lịch sử phục vụ & kết quả chuẩn bị

```json
{
  "eventId": "guid", "eventDate": "2026-09-27", "time": "07:00:00", "title": "Chúa nhật XXVI Thường niên",
  "liturgicalSeasonName": "Mùa Thường niên", "locationName": "Nhà thờ chính",
  "songCount": 5, "membersServed": 18,
  "participationConfirmed": 25, "participationDeclined": 2,
  "rehearsalsHeld": 2, "attendancePresent": 51
}
```

- Mỗi dòng một sự kiện, mới nhất trước. `title`, `liturgicalSeasonName` có thể `null`.
- `songCount`: số bài trong danh sách bài hát `Approved`; `0` nếu không có bản nào được duyệt.
- `membersServed`: số ca viên khác nhau có dòng phân công `Active` trên roster `Finalized`; roster chưa chốt → `0`.
- `rehearsalsHeld`: buổi tập gắn với sự kiện đã bắt đầu; `attendancePresent`: tổng lượt `Present` + `Late` ở các buổi đó.
- Số liệu chi tiết của một sự kiện (thiếu người, bài tập đạt / quá hạn): `GET /api/liturgical-events/{id}/preparation-status`.

### `SongUsageDto` — tần suất dùng bài hát

```json
{ "songId": "guid", "title": "Kinh Hoà Bình", "composer": "Kim Long", "timesUsed": 4, "lastUsedDate": "2026-09-27" }
```

- `timesUsed`: số **sự kiện** có bài này trong danh sách bài hát `Approved`; một bài hát hai lần trong cùng sự kiện tính 1.
- Sắp theo `timesUsed` giảm dần rồi theo tên. Bài chưa dùng lần nào vẫn có trong danh sách với `timesUsed = 0`,
  `lastUsedDate = null`. Bài đã xoá khỏi thư viện chỉ hiện khi từng được dùng trong khoảng lọc.
- Bộ lọc chỉ thu hẹp tập sự kiện được đếm, không bỏ bài hát khỏi danh sách.

---

## 4d. Audit logs — `api/audit-logs` · role `Admin` (UC-35 / FE-54)

| Method | Route | Query | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/audit-logs` | `userId`, `entityType`, `entityId`, `action`, `fromDate`, `toDate`, `pageNumber`, `pageSize` | 200 `PagedList<AuditLogDto>` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) |

- Mới nhất trước. Các bộ lọc đều tuỳ chọn, kết hợp AND, so khớp chính xác. `userId` là **người thực hiện**;
  `fromDate`, `toDate` bao gồm hai đầu (`yyyy-MM-dd`, giờ Việt Nam). `fromDate` > `toDate` → 400 ở trường `toDate`.
- Role khác gọi vào → `403` body rỗng.

### `AuditLogDto`

```json
{
  "id": "guid", "createdAt": "2026-10-11T08:30:00Z",
  "userId": "guid", "userFullName": "Quản trị viên", "userEmail": "admin@example.com",
  "action": "Updated", "entityType": "User", "entityId": "guid",
  "oldValue": "{\"IsActive\":true}", "newValue": "{\"IsActive\":false}",
  "ipAddress": "203.0.113.5"
}
```

- `action`: `Created`, `Updated`, `Deleted`, `PasswordChanged`.
- `entityType` đang được ghi: `User`, `Role`, `SystemSetting`, `SongList`, `SongListReview`, `ServiceRoster`,
  `RosterAssignment`, `MemberSkill`, `LiturgicalEvent`, `MusicMaterial`, `RehearsalAttendance`, `Skill`,
  `SkillCategory`, `MassType`, `CeremonyType`, `EventCategory`, `LiturgicalSeason`, `SongTheme`, `LiturgicalSlot`,
  `WorshipLocation`.
- `oldValue` / `newValue` là **chuỗi** chứa một object JSON, FE tự parse. `Updated` chỉ gồm các trường đã đổi;
  `Created` chỉ có `newValue`; `Deleted` chỉ có `oldValue`; `PasswordChanged` không có cả hai.
  Tên trường là tên property phía server (PascalCase); enum ghi bằng tên.
- Mật khẩu và token không bao giờ xuất hiện trong hai trường này.
- `userId`, `userFullName`, `userEmail` là `null` khi không ai đăng nhập lúc thay đổi xảy ra — ví dụ
  `PasswordChanged` qua link đặt lại mật khẩu. `PasswordChanged` có `userId` là người dùng tự đổi.
- Xoá tư liệu hiện ra là `MusicMaterial` + `Updated` với `newValue` = `{"IsActive":false}`.
- `RehearsalAttendance` chỉ được ghi khi **sửa trạng thái** của một dòng đã điểm danh; lần điểm danh đầu không ghi.
- Đăng nhập, đăng xuất không được ghi.

---

## 4e. System settings — `api/system-settings` · sửa: `Admin` · đọc cài đặt công khai: mọi role, một phần không cần đăng nhập (UC-33 / FE-51)

| Method | Route | Role | Body | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/system-settings` | `Admin` | — | 200 `SystemSettingDto[]` | — |
| GET | `/api/system-settings/public` | mọi role · **không cần đăng nhập** | — | 200 `SystemSettingDto[]`: đã đăng nhập → mọi key công khai; chưa đăng nhập → chỉ `Parish.Name`, `Choir.Name`, `Contact.Email` | — |
| PUT | `/api/system-settings/{id}` | `Admin` | `{ "value" }` | 200 `SystemSettingDto` | 400 `VALIDATION_FAILED`, `SETTING_VALUE_TYPE_MISMATCH`, `SETTING_VALUE_OUT_OF_RANGE` · 404 `SETTING_NOT_FOUND` |

```json
{
  "id": "guid", "key": "Roster.RecentServiceDays", "value": "60", "dataType": "Int",
  "displayName": "Số ngày tính là phục vụ gần đây", "group": "Phân công", "order": 4,
  "description": "Số ngày gần nhất được tính là đã phục vụ gần đây khi đề xuất phân công.",
  "isPublic": false, "minValue": 1, "maxValue": 365
}
```

- Danh sách sắp theo `order` (thứ tự trên trang cấu hình, xuyên suốt các nhóm). `displayName` và `group` là tiếng Việt,
  FE hiển thị nguyên văn. **Bộ key do hệ thống định nghĩa**: Admin chỉ sửa `value`, không tạo, không xoá, không đổi kiểu.
- `value` luôn là **chuỗi**, đọc theo `dataType`: `Int` → `"42"`, `Bool` → `"true"` / `"false"`, `String` → nguyên văn
  (được để rỗng), `Json` → một giá trị JSON hợp lệ. Server cắt khoảng trắng hai đầu và chuẩn hoá (`" 042 "` → `"42"`,
  `"True"` → `"true"`). Tối đa 1000 ký tự.
- Sai kiểu → 400 `SETTING_VALUE_TYPE_MISMATCH`. Số nguyên ngoài `minValue`–`maxValue` → 400 `SETTING_VALUE_OUT_OF_RANGE`.
  `minValue`, `maxValue` là `null` với key không phải số.
- `description` là câu giải thích tiếng Việt, hiển thị dưới ô nhập.
- Mọi thay đổi được ghi vào nhật ký hoạt động (`entityType` = `SystemSetting`).

| `key` | Kiểu | Mặc định | Công khai | Tác dụng |
|---|---|---|---|---|
| `Parish.Name` | String | rỗng | có | Tên giáo xứ để hiển thị |
| `Choir.Name` | String | rỗng | có | Tên ca đoàn để hiển thị |
| `System.Announcement` | String | rỗng | có | Thông báo chung cho mọi người dùng; rỗng thì không hiện |
| `Roster.RecentServiceDays` | Int 1–365 | 60 | không | Số ngày tính "phục vụ gần đây" khi gợi ý phân công |
| `Roster.AiSuggestionEnabled` | Bool | true | không | Tắt thì gợi ý phân công không gọi AI, chỉ xếp theo luật (`isAiGenerated` = `false`) |
| `Roster.EventOverlapMinutes` | Int 0–720 | 120 | không | Không phân công một ca viên vào hai lễ cùng ngày bắt đầu cách nhau ít hơn N phút (`ROSTER_MEMBER_TIME_CONFLICT`; gợi ý bỏ qua người đó). 0 = chỉ chặn hai lễ cùng giờ |
| `Practice.AllowLateSubmission` | Bool | false | không | Bật thì nộp bản thu sau hạn không còn bị `PRACTICE_SUBMISSION_PAST_DUE`; bài vẫn tính là quá hạn |
| `Rehearsal.AttendanceOpensMinutesBefore` | Int 0–120 | 0 | không | Cho điểm danh sớm N phút trước giờ bắt đầu; sớm hơn nữa vẫn `REHEARSAL_NOT_STARTED` |

---

## 4f. Choirs — `api/choirs` · tạo / sửa: `Admin` · xem, quản lý ca viên: `Admin`, `ChoirDirector` · xem danh sách: `ParishPriest` · ca đoàn phục vụ sự kiện `api/liturgical-events/{id}/choirs`: gán `ParishPriest`, xem `ParishPriest`, `ChoirDirector` (D7, D8)

Một giáo xứ có nhiều ca đoàn; ca viên gia nhập được nhiều ca đoàn, một sự kiện có nhiều ca đoàn phục vụ.
Mỗi ca đoàn có **một** ca trưởng do Admin gán (`directorId`); một ca trưởng có thể phụ trách nhiều ca đoàn.
Ca trưởng chỉ thấy và quản lý ca đoàn của mình — ca đoàn của ca trưởng khác trả 404 `CHOIR_NOT_FOUND`.
Admin thấy và quản lý mọi ca đoàn.

### `ChoirDto` / `ChoirMemberDto`

```json
{ "id": "guid", "name": "Ca đoàn Cêcilia", "description": "Ca đoàn giới trẻ", "isActive": true,
  "directorId": "guid", "directorFullName": "Nguyễn Văn A" }
```

```json
{ "memberId": "guid", "userId": "guid", "fullName": "Nguyễn Văn A", "email": "a@b.com", "joinedDate": "2026-10-17" }
```

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| POST | `/api/choirs` | `Admin` | `{ "name", "description?", "directorId?" }` | 200 `ChoirDto` | 400 `VALIDATION_FAILED`, `CHOIR_DIRECTOR_INVALID` · 409 `CHOIR_NAME_ALREADY_EXISTS` |
| GET | `/api/choirs` | `Admin`, `ChoirDirector`, `ParishPriest` | — | 200 `ChoirDto[]` (sắp theo tên, gồm cả ca đoàn đã tắt; ca trưởng chỉ nhận ca đoàn của mình, Admin và cha xứ nhận mọi ca đoàn) | — |
| PUT | `/api/choirs/{id}` | `Admin` | `{ "name", "description?", "isActive", "directorId?" }` | 200 `ChoirDto` | 400 `VALIDATION_FAILED`, `CHOIR_DIRECTOR_INVALID` · 404 `CHOIR_NOT_FOUND` · 409 `CHOIR_NAME_ALREADY_EXISTS` |
| GET | `/api/choirs/{id}/members` | `Admin`, `ChoirDirector` | — | 200 `ChoirMemberDto[]` (ca viên đang ở trong ca đoàn, sắp theo tên) | 404 `CHOIR_NOT_FOUND` |
| POST | `/api/choirs/{id}/members` | `Admin`, `ChoirDirector` | `{ "memberIds": ["guid"] }` | 204 | 400 `VALIDATION_FAILED` (`CHOIR_MEMBER_INVALID`), `CHOIR_MEMBER_INVALID` · 404 `CHOIR_NOT_FOUND` · 409 `CHOIR_INACTIVE` |
| DELETE | `/api/choirs/{id}/members/{memberId}` | `Admin`, `ChoirDirector` | — | 204 | 404 `CHOIR_NOT_FOUND`, `CHOIR_MEMBERSHIP_NOT_FOUND` |
| GET | `/api/liturgical-events/{id}/choirs` | `ParishPriest`, `ChoirDirector` | — | 200 `LiturgicalEventChoirDto[]` (ca đoàn phụ trách trước, rồi theo tên) | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| PUT | `/api/liturgical-events/{id}/choirs` | `ParishPriest` | `{ "choirIds": ["guid"], "leadChoirId?" }` | 200 `LiturgicalEventChoirDto[]` (danh sách sau khi lưu) | 400 `VALIDATION_FAILED` (`CHOIR_LEAD_INVALID`) · 404 `LITURGICAL_EVENT_NOT_FOUND`, `CHOIR_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `CHOIR_INACTIVE` |

- `name` bắt buộc, ≤ 100 ký tự, không trùng ca đoàn khác; `description` ≤ 500 ký tự.
- `directorId` là id `User` (lấy từ `GET /api/users?roleName=ChoirDirector`); bỏ trống / `null` = chưa có ca trưởng.
  Không phải tài khoản `ChoirDirector` đang hoạt động → 400 `CHOIR_DIRECTOR_INVALID`. `PUT` giữ nguyên ca trưởng cũ
  thì không bị kiểm lại (kể cả khi tài khoản đó đã bị khoá). `PUT` gửi `directorId: null` là gỡ ca trưởng.
- `isActive = false` là tắt ca đoàn: giữ ca viên và sự kiện đã gán, nhưng không nhận thêm ca viên
  (409 `CHOIR_INACTIVE`) và không gán được vào sự kiện mới.
- `memberIds` là id `MemberProfile` (lấy từ `GET /api/member-profiles`); id trùng tự loại. Có id nào không tồn tại
  hoặc ca viên không ở trạng thái `Active` → 400 `CHOIR_MEMBER_INVALID`, không lưu gì.
  Ca viên đã ở trong ca đoàn thì bỏ qua; ca viên từng rời ca đoàn thì vào lại, `joinedDate` = hôm nay.
- `DELETE .../members/{memberId}`: ca viên rời ca đoàn, lịch sử vẫn giữ; thêm lại bằng `POST .../members`.
- `PUT /api/liturgical-events/{id}/choirs` **thay toàn bộ** danh sách ca đoàn của sự kiện; gửi `[]` là gỡ hết.
  Sự kiện đã huỷ hoặc đã qua ngày thì không sửa được. Ca đoàn đã tắt vẫn được giữ lại nếu đã gán từ trước,
  nhưng không thêm mới được.
- `leadChoirId` là **ca đoàn phụ trách** (D8): bắt buộc khi `choirIds` không rỗng và phải nằm trong `choirIds`;
  gửi `choirIds: []` thì bỏ trống. Sai → 400 `CHOIR_LEAD_INVALID`.

```json
{ "choirId": "guid", "name": "Ca đoàn Cêcilia", "isActive": true, "isLead": true,
  "directorId": "guid", "directorFullName": "Nguyễn Văn A" }
```

### Phạm vi của ca trưởng (D8)

Mỗi ca đoàn có **một** ca trưởng; ca trưởng chỉ quản lý ca đoàn của mình. Admin, cha xứ không bị giới hạn.
Bản ghi **chưa gắn ca đoàn** (dữ liệu trước D8: buổi tập / bài tập `choirId = null`, sự kiện chưa gán ca đoàn nào)
thì ca trưởng nào cũng quản lý được.

| Nhóm | Ca trưởng thấy / làm được | Ngoài phạm vi |
|---|---|---|
| Ca viên, kỹ năng, tiến độ học tư liệu, 3 báo cáo theo ca viên | Ca viên ở ca đoàn của mình hoặc chưa ở ca đoàn nào (tìm kiếm `GET /api/member-profiles` vẫn toàn giáo xứ) | 404 `MEMBER_NOT_FOUND` / `MEMBER_SKILL_NOT_FOUND`; báo cáo lọc `choirId` của người khác → 404 `CHOIR_NOT_FOUND` |
| Buổi tập, bài hát buổi tập, điểm danh | Buổi tập có `choirId` thuộc mình. Tạo mới **bắt buộc** `choirId` của mình; gắn sự kiện thì ca đoàn đó phải phục vụ sự kiện. Danh sách điểm danh và thông báo chỉ gồm ca viên của ca đoàn | 404 `REHEARSAL_NOT_FOUND`; tạo: 404 `CHOIR_NOT_FOUND`, 409 `CHOIR_INACTIVE`, `CHOIR_NOT_SERVING_EVENT`; điểm danh người ngoài ca đoàn → 404 `MEMBER_NOT_FOUND` |
| Bài tập về nhà, bản nộp | Bài tập có `choirId` thuộc mình. Tạo mới **bắt buộc** `choirId`; `All` = cả ca đoàn, `SkillGroup` / `Individual` chỉ trong ca đoàn | 404 `PRACTICE_ASSIGNMENT_NOT_FOUND` / `PRACTICE_SUBMISSION_NOT_FOUND`; chỉ định người ngoài ca đoàn → 404 `MEMBER_NOT_FOUND` |
| Sự kiện (danh sách, tiến độ chuẩn bị) | Sự kiện có ca đoàn của mình phục vụ, hoặc chưa gán ca đoàn | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| Danh sách bài, yêu cầu nhân sự, xác nhận tham gia, phân công | **Xem**: ca trưởng mọi ca đoàn phục vụ. **Tạo / sửa / nộp / mời / phân công / chốt / gửi thông báo**: chỉ ca trưởng của ca đoàn phụ trách (sự kiện có ca đoàn nhưng chưa chọn phụ trách: ca trưởng mọi ca đoàn phục vụ). Người được mời / phân công phải là ca viên của các ca đoàn phục vụ | Không phục vụ: 404 như cũ (`LITURGICAL_EVENT_NOT_FOUND`, `SONG_LIST_NOT_FOUND`, `ROSTER_NOT_FOUND`…); phục vụ nhưng không phụ trách: 403 `CHOIR_NOT_LEAD`; người ngoài ca đoàn: 404 `MEMBER_NOT_FOUND` |
| Lịch sắp tới `GET /api/schedule/rehearsals` | Ca viên: buổi tập của ca đoàn mình; ca trưởng: của ca đoàn mình phụ trách; cả hai cộng buổi tập chưa gắn ca đoàn | — |

`SongList.choirId` được gán bằng ca đoàn phụ trách lúc tạo. Ca viên chỉ nhận bài tập của ca đoàn mình đang ở.

---

## 4g. Mass schedules — `api/mass-schedules` · xem: `Admin`, `ParishPriest` · sửa: `Admin` (UC-12)

Giờ lễ cố định hằng tuần của giáo xứ (vd Chúa nhật 04:00 · 07:00 · 15:00). Cha xứ dùng chúng để sinh sự kiện nháp qua
`POST /api/liturgical-events/generation` (mục 4b).

### `MassScheduleDto`

```json
{
  "id": "guid",
  "dayOfWeek": "Sunday",
  "time": "04:00:00",
  "massTypeId": "guid",
  "locationId": "guid",
  "title": "Lễ sáng sớm",
  "isActive": true
}
```

`dayOfWeek`: `Sunday` | `Monday` | `Tuesday` | `Wednesday` | `Thursday` | `Friday` | `Saturday`.

| Method | Route | Quyền | Body | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/mass-schedules` | `Admin`, `ParishPriest` | — | 200 `MassScheduleDto[]` (sắp theo thứ — Chúa nhật trước — rồi giờ) | — |
| POST | `/api/mass-schedules` | `Admin` | `{ "dayOfWeek", "time", "massTypeId", "locationId", "title?" }` | 200 `MassScheduleDto` | 400 `VALIDATION_FAILED` · 404 `LOOKUP_NOT_FOUND` · 409 `LOOKUP_INACTIVE`, `MASS_SCHEDULE_SLOT_TAKEN` |
| PUT | `/api/mass-schedules/{id}` | `Admin` | như `POST`, thêm `"isActive"` | 200 `MassScheduleDto` | 400 `VALIDATION_FAILED` · 404 `MASS_SCHEDULE_NOT_FOUND`, `LOOKUP_NOT_FOUND` · 409 `LOOKUP_INACTIVE`, `MASS_SCHEDULE_SLOT_TAKEN` |

- `GET` trả cả giờ lễ đã tắt. **Không có xoá**: muốn bỏ thì `PUT` với `isActive: false`; giờ lễ tắt không sinh sự kiện.
- `massTypeId`, `locationId` bắt buộc, phải trỏ tới mục có thật và đang hoạt động. Khi `PUT`, Id giữ nguyên so với hiện
  tại vẫn được chấp nhận dù mục đã tắt. `title` tối đa 200 ký tự, được cắt khoảng trắng; chuỗi rỗng lưu thành `null`.
- Trùng cả `dayOfWeek` + `time` + `locationId` với giờ lễ khác → 409 `MASS_SCHEDULE_SLOT_TAKEN`.
- Sửa giờ lễ không đụng tới các sự kiện đã sinh trước đó.

## 4h. Liturgical name translations — `api/liturgical-name-translations` · role `Admin`

Tên tiếng Việt cho các từ tiếng Anh của lịch phụng vụ. Mọi role đọc kết quả qua các trường `…Vi` của `LiturgicalDayDto`.

```json
{ "id": "guid", "kind": "Celebration", "englishName": "Christ the King", "vietnameseName": "Đức Giêsu Kitô, Vua Vũ Trụ" }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/liturgical-name-translations?keyword=&kind=&pageNumber=&pageSize=` | — | 200 `PagedList<LiturgicalNameTranslationDto>` | — |
| GET | `/api/liturgical-name-translations/missing` | — | 200 `[{ "kind", "englishName" }]` — tên của các ngày đã đồng bộ còn hiện tiếng Anh | — |
| POST | `/api/liturgical-name-translations` | `{ "kind", "englishName", "vietnameseName" }` | 200 `LiturgicalNameTranslationDto` | 400 `VALIDATION_FAILED` · 409 `LITURGICAL_NAME_TRANSLATION_DUPLICATE` |
| PUT | `/api/liturgical-name-translations/{id}` | như POST | 200 `LiturgicalNameTranslationDto` | 400 `VALIDATION_FAILED` · 404 `LITURGICAL_NAME_TRANSLATION_NOT_FOUND` · 409 `LITURGICAL_NAME_TRANSLATION_DUPLICATE` |
| DELETE | `/api/liturgical-name-translations/{id}` | — | 204 | 404 `LITURGICAL_NAME_TRANSLATION_NOT_FOUND` |

- `kind`: `Celebration` (tên lễ) · `Rank` (`Solemnity`, `Feast`, `Obligatory Memorial`, `Optional Memorial`) ·
  `Season` (`Advent`, `Christmas`, `Lent`, `Easter`, `OrdinaryTime`). Cùng một từ có thể khác nghĩa theo `kind`
  ("Christmas" là lễ Chúa Giáng Sinh và cũng là Mùa Giáng Sinh), nên duy nhất theo cặp `kind` + `englishName`.
- `englishName` phải đúng nguyên văn như lịch trả (lấy từ `GET missing`); hai tên ≤ 200 ký tự, được cắt khoảng trắng.
- Lần khởi động đầu, bảng rỗng được nạp sẵn 230 bản dịch (221 tên lễ chuyển từ Web + 4 bậc lễ + 5 mùa). Sau đó danh sách
  thuộc về Admin: dòng đã xoá không được nạp lại.
- Ngày thường và Chúa nhật theo mẫu (thứ trong tuần, tuần thứ mấy của mùa) được dịch bằng quy tắc, không cần dòng
  trong bảng; thêm dòng cùng tên thì dòng của Admin được ưu tiên.

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
`PracticeFeedback` | `DirectorNote` | `SkillReview` | `EventCancelled` | `SongListSubmitted` | `RehearsalChanged`. `referenceType` + `referenceId` (nullable) cho biết bấm vào
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

## 5a. Director notes — `api/director-notes` · gửi: `ParishPriest` · đọc: `ParishPriest`, `ChoirDirector` (UC-17 / FE-23)

### `DirectorNoteDto`

```json
{
  "id": "guid",
  "noteDate": "2026-12-24",
  "eventId": "guid",
  "eventTitle": "Thánh lễ Giáng Sinh",
  "fromUserId": "guid", "fromUserName": "Cha Giuse",
  "toUserId": "guid", "toUserName": "Anh Phêrô",
  "content": "Xin ca đoàn chuẩn bị thêm bài Đáp ca...",
  "sentAt": "2026-10-09T08:00:00Z"
}
```

| Method | Route | Role | Body / Query | Thành công | Lỗi |
|---|---|---|---|---|---|
| GET | `/api/director-notes/recipients` | `ParishPriest` | — | 200 `UserSummaryDto[]` (ca trưởng đang hoạt động) | — |
| POST | `/api/director-notes` | `ParishPriest` | `{ "noteDate?", "eventId?", "toUserIds": ["guid"], "content" }` | 200 `DirectorNoteDto[]` | 400 `VALIDATION_FAILED` (`DIRECTOR_NOTE_TARGET_REQUIRED`, `DIRECTOR_NOTE_RECIPIENT_INVALID`), `DIRECTOR_NOTE_RECIPIENT_INVALID` · 404 `LITURGICAL_EVENT_NOT_FOUND` |
| GET | `/api/director-notes?eventId=&pageNumber=&pageSize=` | cả hai | — | 200 `PagedList<DirectorNoteDto>` | — |
| GET | `/api/director-notes/{id}` | cả hai | — | 200 `DirectorNoteDto` | 404 `DIRECTOR_NOTE_NOT_FOUND` |

- Phải có `noteDate` **hoặc** `eventId` (có cả hai cũng được). `content` bắt buộc, ≤ 2000 ký tự.
- `toUserIds`: chọn một hoặc nhiều ca trưởng từ `recipients`; id trùng tự loại. Người nhận nào không phải ca trưởng
  đang hoạt động → 400 `DIRECTOR_NOTE_RECIPIENT_INVALID`, không lưu gì.
- Mỗi ca trưởng nhận **một bản ghi chú riêng** (kết quả `POST` là mảng, mỗi phần tử một người) và một notification
  `type = DirectorNote`, `referenceType = "DirectorNote"`, `referenceId` = id bản ghi chú của chính người đó;
  `content` của notification là 200 ký tự đầu, đọc đủ bằng `GET /api/director-notes/{referenceId}`.
- `GET` danh sách: cha xứ thấy ghi chú **đã gửi**, ca trưởng thấy ghi chú **nhận được**; mới nhất trước.
  Ghi chú giữa người khác → 404 `DIRECTOR_NOTE_NOT_FOUND`.

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
  "notes": null,
  "materialCounts": { "SheetMusic": 1, "Lyrics": 0, "SampleAudio": 2, "RehearsalMaterial": 0 },
  "isClassified": true,
  "timesUsed": 3,
  "lastUsedDate": "2026-10-18"
}
```

- `materialCounts`: số tài liệu đang hoạt động theo từng `MaterialType`, đủ mọi loại (0 khi chưa có).
- `isClassified`: bài đã có ít nhất một mùa phụng vụ, loại lễ, nghi thức, chủ đề hoặc vị trí trong lễ.
- `timesUsed` / `lastUsedDate`: đếm như báo cáo `song-usage` — sự kiện `Published` đã qua có danh sách `Approved`
  chứa bài; `lastUsedDate` là `null` khi `timesUsed` = 0.

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/songs?keyword=&liturgicalSeasonId=&massTypeId=&ceremonyTypeId=&songThemeId=&liturgicalSlotId=&skillId=&isClassified=&missingMaterialType=&pageNumber=1&pageSize=20` | — | 200 `PagedList<SongDto>` | — |
| GET | `/api/songs/{id}` | — | 200 `SongDto` | 404 `SONG_NOT_FOUND` |
| POST | `/api/songs` | `{ "title", "composer", "lyricist", "musicalKey", "tempo", "notes" }` | 200 `SongDto` | 400 `VALIDATION_FAILED` · 409 `SONG_TITLE_DUPLICATE` |
| PUT | `/api/songs/{id}` | như POST | 200 `SongDto` | 400 `VALIDATION_FAILED` · 404 `SONG_NOT_FOUND` · 409 `SONG_TITLE_DUPLICATE` |
| DELETE | `/api/songs/{id}` | — | 204 | 404 `SONG_NOT_FOUND` · 409 `SONG_IN_USE` |
| GET | `/api/songs/{id}/classification` | — | 200 `SongClassificationDto` | 404 `SONG_NOT_FOUND` |
| PUT | `/api/songs/{id}/classification` | `UpdateSongClassificationRequest` | 200 `SongClassificationDto` | 400 `VALIDATION_FAILED`, `SONG_CLASSIFICATION_DUPLICATE`, `SONG_SKILL_REQUIREMENT_DUPLICATE`, `SONG_CLASSIFICATION_TARGET_INVALID`, `SONG_SKILL_REQUIREMENT_CATEGORY_INVALID` · 404 `SONG_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE` |

- `songThemeId` lấy cả bài gắn chủ đề con cháu (lọc "Đức Maria" ra cả bài gắn "Mân Côi"). `liturgicalSlotId`
  lọc theo vị trí trong lễ (id từ `GET /api/lookups/liturgical-slots`).
- `UpdateSongClassificationRequest` có thêm `liturgicalSlotIds` (Nhập lễ, Đáp ca, Dâng lễ, Hiệp lễ, Kết lễ...);
  `SongClassificationDto` có thêm `liturgicalSlots`. Luật trùng / không hợp lệ như các nhóm khác.
- `keyword` tìm trong `title`, `composer`, `lyricist`, **không phân biệt hoa thường và dấu tiếng Việt**
  ("con buoc" ra "Con Bước", "duc" ra "Đức"); kết quả sắp theo `title`. Tìm ca viên theo tên
  (`/api/member-profiles`) và tìm tài liệu (`/api/music-materials/mine`) cũng không phân biệt dấu.
- `isClassified=false` chỉ lấy bài chưa phân loại (`true`: đã phân loại). `missingMaterialType=SampleAudio` chỉ lấy bài
  chưa có tài liệu đang hoạt động thuộc loại đó.
- Chỉ `title` bắt buộc (≤ 200). `composer`, `lyricist` ≤ 150 · `musicalKey` ≤ 10 · `tempo` ≤ 50 ·
  `notes` ≤ 1000. Chuỗi được trim, chuỗi rỗng lưu thành `null`.
- `SONG_TITLE_DUPLICATE`: đã có bài cùng `title` **và** cùng `composer` (cùng tên khác nhạc sĩ vẫn được).
- `DELETE` là xoá mềm, không khôi phục được: bài biến khỏi danh sách và mọi route theo `id`
  trả `SONG_NOT_FOUND`. Tạo lại bài cùng tên được. Bài **đã được dùng** — nằm trong một danh sách bài hát bất kỳ
  (kể cả bản nháp hay bị từ chối), một buổi tập hoặc một bài luyện tập — không xoá được: 409 `SONG_IN_USE`.
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
  "vocalRequirements": [{ "skillId": "guid", "skillName": "Soprano", "isMandatory": true, "requiredCount": 1 }],
  "instrumentRequirements": [{ "skillId": "guid", "skillName": "Organ", "isMandatory": false, "requiredCount": 1 }]
}
```

```json
{
  "liturgicalSeasonIds": ["guid"],
  "massTypeIds": [],
  "ceremonyTypeIds": [],
  "songThemeIds": ["guid"],
  "vocalRequirements": [{ "skillId": "guid", "isMandatory": true, "requiredCount": 1 }],
  "instrumentRequirements": [{ "skillId": "guid", "isMandatory": false, "requiredCount": 1 }]
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
- `requiredCount` (1–50, mặc định 1): số người bài cần với kỹ năng đó — song ca 2, Thương Khó 3 solo. Ngoài khoảng →
  400 `PERSONNEL_REQUIRED_COUNT_INVALID`.
- **Điền sẵn khi lập danh sách bài**: mục `isMandatory = true` thuộc nhóm **khác Vocal** (Solo, Psalm, Instrument,
  Conducting support) được chép sang yêu cầu nhân sự của bài khi bài được đưa vào danh sách. Bè Vocal (S/A/T/B) không bao
  giờ được chép — bài đồng ca không sinh phân công.

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
| PUT | `/api/music-materials/{id}/file` | `multipart/form-data`: `file`, `resetProgress?` (mặc định `false`) | 200 `MusicMaterialDto` — giữ nguyên `id`, tiêu đề, loại, kỹ năng đích và bài luyện tập đang gắn; file cũ bị xoá khỏi kho. `resetProgress=true` xoá đánh dấu "đã học / cần luyện thêm" của ca viên với tài liệu này | 400 `MATERIAL_FILE_REQUIRED`, `MATERIAL_FILE_TYPE_NOT_ALLOWED` (theo loại hiện có của tài liệu) · 404 `MATERIAL_NOT_FOUND` · 413 `MATERIAL_FILE_TOO_LARGE` · 502 `EXTERNAL_STORAGE_FAILED` |
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

## 6a. Song lists — `api/song-lists` · đề xuất: `ChoirDirector` · duyệt: `ParishPriest` · xem bản đã duyệt: mọi role (F6)

### `SongListDto`

```json
{
  "id": "guid", "eventId": "guid", "version": 2, "status": "Submitted",
  "proposedBy": "guid", "previousVersionId": "guid",
  "submittedAt": "2026-10-10T08:00:00Z", "decidedAt": null,
  "items": [
    { "id": "guid", "songId": "guid", "songTitle": "Kinh Hòa Bình", "slotId": "guid", "slotName": "Nhập lễ", "displayOrder": 1, "note": null }
  ],
  "reviews": [
    { "id": "guid", "reviewerId": "guid", "decision": "RequestRevision", "notes": "Đổi bài hiệp lễ", "reviewedAt": "2026-10-09T10:00:00Z" }
  ]
}
```

- `status`: `Draft` | `Submitted` | `Approved` | `Rejected` | `NeedsRevision`.
- `decision`: `Approve` | `Reject` | `RequestRevision`.
- `previousVersionId`, `submittedAt`, `decidedAt`, `note`, `notes` có thể `null`.

### `CreateSongListRequest` / `UpdateSongListItemsRequest` / `ReviewSongListRequest`

```json
{ "eventId": "guid", "items": [{ "songId": "guid", "slotId": "guid", "displayOrder": 1, "note": null }] }
```

```json
{ "items": [{ "songId": "guid", "slotId": "guid", "displayOrder": 1, "note": null }] }
```

```json
{ "decision": "Reject", "notes": "Lý do" }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/song-lists/event/{eventId}/approved` · mọi role | — | 200 `SongListDto` | 404 `SONG_LIST_NOT_FOUND` |
| GET | `/api/song-lists/event/{eventId}/latest` · `ChoirDirector`, `ParishPriest` | — | 200 `SongListDto` | 404 `SONG_LIST_NOT_FOUND` |
| GET | `/api/song-lists/{id}` · `ChoirDirector`, `ParishPriest` | — | 200 `SongListDto` | 404 `SONG_LIST_NOT_FOUND` |
| GET | `/api/song-lists/pending` · `ParishPriest` | — | 200 `SongListDto[]` | — |
| POST | `/api/song-lists` · `ChoirDirector` | `CreateSongListRequest` | 200 `SongListDto` | 400 `VALIDATION_FAILED` (`SONG_LIST_EMPTY`, `SONG_LIST_SLOT_DUPLICATE`) · 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `SONG_NOT_FOUND`, `SLOT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_NOT_PUBLISHED`, `SONG_LIST_EVENT_HAS_APPROVED_VERSION`, `SONG_LIST_CANNOT_BE_REVISED`, `SONG_INACTIVE`, `LOOKUP_INACTIVE` |
| PUT | `/api/song-lists/{id}/items` · `ChoirDirector` | `UpdateSongListItemsRequest` | 200 `SongListDto` | 400 `VALIDATION_FAILED` (`SONG_LIST_EMPTY`, `SONG_LIST_SLOT_DUPLICATE`) · 403 `CHOIR_NOT_LEAD` · 404 `SONG_LIST_NOT_FOUND`, `SONG_NOT_FOUND`, `SLOT_NOT_FOUND` · 409 `SONG_LIST_NOT_LATEST_VERSION`, `SONG_LIST_NOT_EDITABLE`, `SONG_INACTIVE`, `LOOKUP_INACTIVE` |
| POST | `/api/song-lists/{id}/submit` · `ChoirDirector` | — | 200 `SongListDto` | 400 `SONG_LIST_EMPTY` · 403 `CHOIR_NOT_LEAD` · 404 `SONG_LIST_NOT_FOUND` · 409 `SONG_LIST_NOT_LATEST_VERSION`, `SONG_LIST_ALREADY_SUBMITTED`, `SONG_LIST_NOT_EDITABLE` |
| POST | `/api/song-lists/{id}/review` · `ParishPriest` | `ReviewSongListRequest` | 200 `SongListDto` | 400 `VALIDATION_FAILED` (`REVIEW_NOTES_REQUIRED`) · 404 `SONG_LIST_NOT_FOUND` · 409 `SONG_LIST_NOT_SUBMITTED` |

**Vòng đời:** `Draft` → (`submit`) `Submitted` → (`review`) `Approved` | `Rejected` | `NeedsRevision`.

- **Mỗi phiên bản là một bản ghi riêng.** `POST` tạo `version = 1` ở trạng thái `Draft` khi sự kiện chưa có danh sách nào.
  Khi bản mới nhất đang `Rejected` hoặc `NeedsRevision`, `POST` tạo `version + 1` với `previousVersionId` trỏ về bản cũ.
  Bản mới nhất đang `Draft` / `Submitted` → `SONG_LIST_CANNOT_BE_REVISED`; đã `Approved` → `SONG_LIST_EVENT_HAS_APPROVED_VERSION`.
- Chỉ tạo được cho sự kiện `Published`. Sự kiện `Draft` → `LITURGICAL_EVENT_NOT_PUBLISHED`, đã huỷ → `LITURGICAL_EVENT_CANCELLED`.
- `PUT …/items` **thay cả bộ** bài, chỉ khi danh sách là phiên bản mới nhất và còn `Draft`.
- Một `slotId` được có **nhiều bài** (vd Hiệp lễ hát 2 bài), xếp theo `displayOrder`; chỉ cùng một bài lặp lại trong cùng
  một `slotId` mới bị chặn (`SONG_LIST_SLOT_DUPLICATE`), cùng một bài ở hai vị trí khác nhau vẫn được. `items` không được rỗng
  (`SONG_LIST_EMPTY`). Cả hai bắt ở validator, trả 400 `VALIDATION_FAILED`. `slotId` lấy từ `GET /api/lookups/liturgical-slots`.
- Bài hát và vị trí phụng vụ phải đang hoạt động: bài đã tắt → `SONG_INACTIVE`, vị trí đã tắt → `LOOKUP_INACTIVE`.
- `review`: `notes` bắt buộc khi `decision` là `Reject` hoặc `RequestRevision`; `Approve` thì tuỳ chọn.
- `GET …/approved` chỉ trả danh sách `Approved` của sự kiện đang `Published`. Sự kiện còn `Draft`, đã huỷ hoặc không tồn tại
  → 404 `SONG_LIST_NOT_FOUND`, với mọi role. Cha xứ và ca trưởng vẫn xem được bằng `GET /api/song-lists/{id}`.
- `GET …/latest`: phiên bản mới nhất ở **mọi trạng thái**, kèm `items` và `reviews` — ca trưởng dùng để mở lại danh sách
  đang soạn. Riêng `ParishPriest` **không thấy bản `Draft`**: trả phiên bản mới nhất không phải `Draft` (ví dụ ca trưởng
  đang soạn v2 thì cha xứ nhận v1 `NeedsRevision`). Sự kiện chưa có danh sách (hoặc chỉ có `Draft`, với cha xứ) → 404.
- `GET /api/song-lists/{id}`: cùng luật — cha xứ mở id của một bản `Draft` → 404 `SONG_LIST_NOT_FOUND`.
- `GET …/pending`: các danh sách đang `Submitted`, bản gửi sớm nhất trước. **`items` và `reviews` ở đây luôn rỗng** —
  gọi `GET /api/song-lists/{id}` để lấy chi tiết.
- `submit` xong, mọi `ParishPriest` đang hoạt động nhận thông báo `SongListSubmitted`; `review` xong, ca trưởng đề xuất
  nhận thông báo `SongListDecision`. Cả hai có `referenceType = "SongList"`, `referenceId` = id danh sách (S-05).
- Nhân sự cần cho từng bài: xem mục 7a, dùng `items[].id`.

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
| PUT | `/api/song-list-items/{id}/personnel-requirements` | `UpdateSongPersonnelRequirementsRequest` | 200 `SongPersonnelRequirementDto[]` | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `SONG_LIST_ITEM_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE`, `SONG_LIST_NOT_LATEST_VERSION`, `ROSTER_ALREADY_FINALIZED`, `PERSONNEL_REQUIREMENT_IN_USE` |

**Điền sẵn yêu cầu nhân sự** khi tạo danh sách (`POST /api/song-lists`) hoặc sửa bài (`PUT .../items`):
1. Cùng bài hát ở cùng vị trí trong phiên bản trước / bản nháp trước khi sửa → giữ nguyên yêu cầu ca trưởng đã để lại
   (kể cả không có gì). Trước đây sửa danh sách làm mất hết yêu cầu đã khai.
2. Bài mới: lấy yêu cầu `isMandatory` khác bè Vocal ở thư viện + yêu cầu mặc định của vị trí, trùng kỹ năng thì lấy số lớn hơn;
   bỏ kỹ năng sự kiện đã khai cả buổi lễ.
Ca trưởng vẫn sửa được bằng `PUT /api/song-list-items/{id}/personnel-requirements`.

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
    "songListItemId": "guid", "songTitle": "Hãy Nâng Tâm Hồn", "note": null, "source": "Suggested"
  }],
  "shortages": [{
    "songListItemId": "guid", "songTitle": "Hãy Nâng Tâm Hồn",
    "skillId": "guid", "skillName": "Guitar", "requiredCount": 2, "assignedCount": 1
  }]
}
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/service-rosters/suggestions` | `{ "eventId": "guid" }` | 200 `RosterSuggestionResponse` | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `ROSTER_NO_PERSONNEL_REQUIREMENT`, `ROSTER_ALREADY_FINALIZED` |
| GET | `/api/service-rosters?eventId=` | — | 200 `ServiceRosterDto` (assignment `Active` + shortage; `shortages` rỗng nếu sự kiện không còn song list đã duyệt) | 404 `LITURGICAL_EVENT_NOT_FOUND`, `ROSTER_NOT_FOUND` (chưa có roster — gọi `suggestions` hoặc thêm tay) |
| GET | `/api/service-rosters/shortages?eventId=` | — | 200 `RosterShortageDto[]` | 404 `LITURGICAL_EVENT_NOT_FOUND` · 409 `ROSTER_SONG_LIST_NOT_APPROVED` |
| POST | `/api/service-rosters/assignments` | `{ "eventId", "songListItemId?", "skillId", "memberId", "note?" }` | 200 `RosterAssignmentDto` | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `PERSONNEL_REQUIREMENT_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `ROSTER_ASSIGNMENT_DUPLICATE`, `MEMBER_NOT_ACTIVE`, `ROSTER_ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED`, `ROSTER_MEMBER_TIME_CONFLICT` |
| POST | `/api/service-rosters/assignments/{id}/replacement` | `{ "memberId", "note?" }` | 200 `RosterAssignmentDto` (dòng mới) | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_ASSIGNMENT_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_ASSIGNMENT_DUPLICATE`, `MEMBER_NOT_ACTIVE`, `ROSTER_ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED`, `ROSTER_MEMBER_TIME_CONFLICT` |
| PUT | `/api/service-rosters/assignments/{id}/note` | `{ "note" }` | 200 `RosterAssignmentDto` | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_ASSIGNMENT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED` |
| DELETE | `/api/service-rosters/assignments/{id}` | — | 204 | 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_ASSIGNMENT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED` |
| POST | `/api/service-rosters/{id}/finalization` | — | 200 `ServiceRosterDto` | 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_SONG_LIST_NOT_APPROVED`, `MEMBER_NOT_ACTIVE`, `ROSTER_ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED` |
| POST | `/api/service-rosters/{id}/notifications` | `{ "memberIds": ["guid"] }` | 204 | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_NOT_FOUND`, `ROSTER_ASSIGNMENT_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_NOT_FINALIZED` |

`ServiceRosterDto` có thêm `wholeServiceFinalizedAt` (null khi chưa chốt sớm vai cả buổi lễ).
`RosterAssignmentDto` = một phần tử của `assignments` ở trên. `RosterShortageDto` = một phần tử của `shortages`.
`songListItemId` / `songTitle` là `null` ở dòng phân công và dòng thiếu người **cả buổi lễ** (organ, bắt nhịp...).
`note` là ghi chú vai ("Thuật giả", "câu 2"), tối đa 200 ký tự; `replacement` không gửi `note` thì giữ ghi chú của dòng cũ,
`PUT .../note` gửi rỗng để xoá. `MyAssignmentDto` của ca viên cũng có `note`.

### Ba tầng nhân sự

| Tầng | Ví dụ | Khai ở đâu | Phân công |
|---|---|---|---|
| Đồng ca | hát bè S/A/T/B | không khai | không — chỉ xác nhận tham gia |
| Cả buổi lễ | organ, guitar, bắt nhịp | `PUT /api/liturgical-events/{id}/personnel-requirements` | một người giữ cả lễ, `songListItemId = null` |
| Theo bài | xướng đáp ca, Alleluia, solo, song ca, vai Thương Khó | `PUT /api/song-list-items/{id}/personnel-requirements` (được điền sẵn) | theo từng bài |

- Người cả buổi lễ chơi **mọi bài**, nên bài chỉ cần **phần vượt quá**: bài cần Guitar ×2, cả lễ có Guitar ×1 → bài còn
  Guitar ×1 (gợi ý, thiếu người tính trên phần này, `requiredCount` của dòng thiếu = phần còn lại). Bài cần ≤ số của cả lễ
  → coi là đủ, không gợi ý riêng. Người đang giữ kỹ năng đó cho cả lễ không được chọn lại cho phần thêm của bài — gợi ý tự bỏ qua, thêm / thay tay
  trả 409 `ROSTER_ASSIGNMENT_DUPLICATE` (cả chiều ngược lại: người có dòng ở bài không được làm dòng cả lễ cùng kỹ năng).
- Yêu cầu cả buổi lễ **không cần** danh sách bài đã duyệt: `suggestions`, `shortages`, thêm tay dòng cả buổi lễ chạy được
  ngay. Chỉ báo `ROSTER_SONG_LIST_NOT_APPROVED` khi chưa có danh sách duyệt **và** sự kiện không có yêu cầu cả buổi lễ
  (thêm tay dòng theo bài vẫn cần danh sách duyệt). `finalization` vẫn cần danh sách đã duyệt.

### Yêu cầu nhân sự cả buổi lễ — `api/liturgical-events/{id}/personnel-requirements`

| Method | Role | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `ParishPriest`, `ChoirDirector` | — | 200 `EventPersonnelRequirementDto[]` | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| PUT | `ChoirDirector` (ca đoàn phụ trách) | `{ "requirements": [{ "skillId", "requiredCount" }] }` | 200 `EventPersonnelRequirementDto[]` | 400 `VALIDATION_FAILED` (`PERSONNEL_REQUIREMENT_DUPLICATE`, `PERSONNEL_REQUIRED_COUNT_INVALID`) · 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE`, `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `PERSONNEL_REQUIREMENT_IN_USE` |

`EventPersonnelRequirementDto` = `{ "skillId", "skillName", "skillCategoryId", "requiredCount" }`, sắp theo tên kỹ năng.
`PUT` thay cả bộ; `requiredCount` 1–50. Bỏ một kỹ năng đang có người được gán → 409 `PERSONNEL_REQUIREMENT_IN_USE`
(gỡ dòng phân công trước). Đã chốt sớm vai cả buổi lễ hoặc chốt toàn bộ → 409 `ROSTER_ALREADY_FINALIZED`.

### Chốt sớm vai cả buổi lễ — `POST /api/service-rosters/{id}/whole-service-finalization`

| Thành công | Lỗi |
|---|---|
| 200 `ServiceRosterDto` (`wholeServiceFinalizedAt` có giá trị, `status` giữ nguyên) | 403 `CHOIR_NOT_LEAD` · 404 `ROSTER_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `ROSTER_ALREADY_FINALIZED`, `ROSTER_NO_PERSONNEL_REQUIREMENT`, `MEMBER_NOT_ACTIVE`, `ROSTER_ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED`, `ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED` |

- Dùng khi cần hẹn organist / người bắt nhịp sớm, trước khi danh sách bài được duyệt. Chỉ kiểm các dòng cả buổi lễ
  (như `finalization`); dòng theo bài không bị đụng.
- Sau khi chốt sớm: dòng cả buổi lễ không thêm / xoá / sửa ghi chú được, yêu cầu cả buổi lễ không sửa được
  (409 `ROSTER_ALREADY_FINALIZED`); `suggestions` giữ nguyên các dòng đó. **Vẫn thay người được**
  (`.../replacement`) khi organist báo bận: dòng mới chưa `notifiedAt`, gửi `notifications` cho người mới.
- `notifications` gửi được ngay, nhưng **chỉ** các dòng cả buổi lễ; chốt toàn bộ xong thì gửi được mọi dòng.
  Trước cả hai bước chốt vẫn là 409 `ROSTER_NOT_FINALIZED`.

### Gợi ý bài cho sự kiện — `GET /api/liturgical-events/{id}/song-suggestions?slotId=&pageNumber=1&pageSize=20`

Role `ParishPriest`, `ChoirDirector`. 200 `PagedList<SongSuggestionResponse>` · 400 `VALIDATION_FAILED` · 404 `LITURGICAL_EVENT_NOT_FOUND`.

```json
{ "songId": "guid", "title": "Bánh Trường Sinh", "composer": "Kim Long", "score": 5,
  "matchesSlot": true, "matchesEventType": false, "matchesSeason": true, "timesUsed": 2, "lastUsedDate": "2026-08-02" }
```

- Điểm: khớp vị trí `slotId` +3, khớp loại lễ hoặc nghi thức của sự kiện +2, khớp mùa của sự kiện +2. Chỉ trả bài có điểm > 0.
- **Loại bỏ** bài chỉ được phân loại vào **mùa khác** (bài Giáng Sinh trong Mùa Chay), và khi có `slotId`, bài chỉ được
  phân loại vào **vị trí khác**. Bài chưa phân loại mùa / vị trí coi như dùng được mọi mùa / vị trí.
- Sắp: điểm giảm dần → bài **để trống ít tiêu chí hơn** (cụ thể hơn) lên trước — để trống mùa / loại lễ / vị trí nghĩa là
  hợp mọi thứ, nên kém hơn một lần khớp thật → bài lâu chưa hát lên trước (chưa hát bao giờ đứng đầu) → tên bài. `timesUsed` / `lastUsedDate`
  đếm như thư viện bài hát.
- Chỉ là gợi ý: lập danh sách vẫn chọn được bài bất kỳ.
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
- Ca viên đang có dòng `Active` ở lễ khác (chưa huỷ) cùng ngày, bắt đầu cách nhau ít hơn `Roster.EventOverlapMinutes`
  phút, bị bỏ khỏi ứng viên; thêm / thay tay người đó trả 409 `ROSTER_MEMBER_TIME_CONFLICT`. Chỉ so lễ cùng ngày; khi chốt
  không kiểm lại.
- AI (Gemini) chọn trong ứng viên, ưu tiên level cao và người ít phục vụ trong 60 ngày gần nhất. Server
  kiểm lại mọi lựa chọn của AI, rồi tự bù chỗ còn trống theo cùng tiêu chí. AI lỗi hoặc quá 20 giây thì
  phần bù lo toàn bộ và `isAiGenerated = false`. Endpoint không trả lỗi vì AI.
- AI được biết ô nào là cả buổi lễ (`isWholeService`) và được dặn rải đều, tránh giao thêm bài cho người đã giữ vai cả lễ.
- Một ca viên có thể phục vụ nhiều bài, và nhiều skill trong cùng một bài. Phần bù theo luật **rải đều**: ưu tiên người
  đang giữ ít dòng nhất trong lần gợi ý này, cùng số dòng thì theo level / số lần phục vụ gần đây. Yêu cầu cả buổi lễ
  được xếp trước, nên organist ít khi bị giao thêm solo.
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

- Ca viên được thêm / thay vào phải **đang hoạt động**, đã được **duyệt** skill đó và đã trả lời `Confirmed` **hoặc**
  `Unsure` cho yêu cầu xác nhận tham gia sự kiện (chưa trả lời hoặc `Declined` → `ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED`;
  gợi ý tự động vẫn chỉ lấy `Confirmed`). Luật này cũng được kiểm lại khi chốt (`finalization`). Đã có đúng (bài, skill, ca viên) đó trên bảng → 409 `ROSTER_ASSIGNMENT_DUPLICATE`.
- Thêm: cặp (`songListItemId`, `skillId`) phải là một yêu cầu nhân sự của danh sách đã duyệt, nếu không → 404
  `PERSONNEL_REQUIREMENT_NOT_FOUND`. Được thêm vượt `requiredCount`. Sự kiện chưa có bảng thì tự tạo bảng `Draft`.
  Dòng thêm tay có `source = Manual`.
- Thay: dòng cũ giữ lại làm lịch sử (không còn hiện trong `assignments`), dòng mới `Manual` cùng bài + skill.
  Gợi ý lại sau đó không gợi ý lại người đã bị thay ở vị trí ấy.
- Xoá: xoá hẳn dòng phân công.
- `{id}` phải là dòng còn hiệu lực; dòng đã bị thay hoặc không tồn tại → 404 `ROSTER_ASSIGNMENT_NOT_FOUND`.
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
  nào trên bảng → 404 `ROSTER_ASSIGNMENT_NOT_FOUND`, không gửi cho ai.
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
  "choirId": "guid | null",
  "eventId": "guid | null",
  "songId": "guid | null",
  "materialId": "guid | null",
  "materialIds": ["guid", "guid"],
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
| GET | `/api/practice-assignments?eventId=&pageNumber=&pageSize=` | — | 200 `PagedList<PracticeAssignmentDto>` | — |
| POST | `/api/practice-assignments` | `{ "choirId", "title", "instruction?", "scope", "dueDate", "eventId?", "songId?", "materialIds?", "materialId?", "skillIds?", "memberIds?" }` | 200 `PracticeAssignmentDto` | 400 `VALIDATION_FAILED` (`PRACTICE_DUE_DATE_IN_PAST`, `PRACTICE_TARGET_REQUIRED`) · 404 `LITURGICAL_EVENT_NOT_FOUND`, `SONG_NOT_FOUND`, `MATERIAL_NOT_FOUND`, `SKILL_NOT_FOUND`, `MEMBER_NOT_FOUND`, `CHOIR_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `CHOIR_INACTIVE`, `CHOIR_NOT_SERVING_EVENT`, `SONG_INACTIVE`, `SKILL_INACTIVE` |

- `GET`: bài tập ca trưởng đã giao, tất cả hoặc của một sự kiện (`eventId`), hạn nộp muộn nhất trước.
  Tên bài hát / tư liệu lấy theo `songId` / `materialId`; bài nộp của từng bài tập xem ở `GET …/{id}/submissions`.
- `scope`: `All` | `SkillGroup` | `Individual`.
  - `All`: giao cho mọi ca viên **đang hoạt động**. `skillIds` / `memberIds` bị bỏ qua.
  - `SkillGroup`: `skillIds` bắt buộc. Người nhận là ca viên đang hoạt động đã được **duyệt** ít nhất một skill trong danh sách.
  - `Individual`: `memberIds` (id hồ sơ ca viên) bắt buộc. Ca viên không còn hoạt động → 404 `MEMBER_NOT_FOUND`.
- `title` tối đa 200 ký tự, `instruction` tối đa 1000. `dueDate` là UTC, phải sau thời điểm gửi.
- `eventId`, `songId`, `materialIds` đều tuỳ chọn. Sự kiện đã huỷ → 409 `LITURGICAL_EVENT_CANCELLED`.
- **Nhiều tư liệu** (vd. bản nhạc + audio mẫu): gửi `materialIds` theo thứ tự hiển thị; id trùng bị bỏ. Một tư liệu
  đã tắt, không tồn tại, hoặc không thuộc `songId` khi có `songId` → 404 `MATERIAL_NOT_FOUND`, không lưu gì.
  `materialId` (một tư liệu) vẫn nhận cho client cũ và bị bỏ qua khi có `materialIds`. Trong response, `materialId` là
  phần tử đầu của `materialIds`.
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
  "materials": [
    { "id": "guid", "title": "Bản nhạc", "materialType": "SheetMusic" },
    { "id": "guid", "title": "Audio bè Soprano", "materialType": "SampleAudio" }
  ],
  "latestSubmissionStatus": "Submitted | null",
  "latestSubmittedAt": "2026-10-10T09:00:00Z | null",
  "isOverdue": false,
  "canSubmit": true,
  "cannotSubmitReason": "string | null"
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
  Không bản thu nào mang `status = Overdue`.
- `canSubmit` = `POST {id}/submissions` sẽ nhận bản thu lúc này; ngược lại `cannotSubmitReason` là mã lỗi `POST` sẽ trả:
  `PRACTICE_SUBMISSION_ALREADY_PASSED` (lần mới nhất đã `Passed`) hoặc `PRACTICE_SUBMISSION_PAST_DUE` (quá `dueDate`
  và setting `Practice.AllowLateSubmission` đang tắt). Server đã xét setting; FE bật / tắt nút nộp theo field này,
  không tự suy từ `isOverdue`.
- `materials`: mọi tư liệu gắn với bài tập, theo thứ tự ca trưởng chọn; `[]` khi không có. `materialId` / `materialTitle` /
  `materialType` là tư liệu đầu tiên, giữ cho app cũ. Mở file qua `api/music-materials` bằng `id` của từng tư liệu.

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
  Lần mới nhất đã `Passed` → 409 `PRACTICE_SUBMISSION_ALREADY_PASSED`. Quá `dueDate` → 409 `PRACTICE_SUBMISSION_PAST_DUE`,
  trừ khi Admin bật `Practice.AllowLateSubmission`. Biết trước bằng `canSubmit` của `GET mine` / `GET mine/{id}`.
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

## 7e. Rehearsals — `api/rehearsals` (UC-20 / FE-26, UC-30 / FE-45)

Lên lịch tập và điểm danh chỉ dành cho `ChoirDirector`. Danh sách bài của buổi tập: mọi role đọc, `ChoirDirector` sửa.

### Lên lịch tập — `RehearsalDto`

```json
{
  "id": "guid", "choirId": "guid | null", "eventId": "guid", "locationId": "guid", "locationName": "Nhà thờ chính",
  "startTime": "2026-10-09T12:00:00Z", "endTime": "2026-10-09T13:30:00Z", "note": "tập bè",
  "songs": [{ "songId": "guid", "songTitle": "Kinh Hòa Bình", "displayOrder": 1, "note": "tập kỹ điệp khúc" }]
}
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/rehearsals` · `ChoirDirector` | `{ "choirId", "eventId?", "locationId?", "startTime", "endTime", "note?", "songs": [{ "songId", "note" }] }` | 200 `RehearsalDto` | 400 `VALIDATION_FAILED` (`REHEARSAL_TIME_INVALID`, `REHEARSAL_SONG_DUPLICATE`) · 404 `LITURGICAL_EVENT_NOT_FOUND`, `LOOKUP_NOT_FOUND`, `SONG_NOT_FOUND`, `CHOIR_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `CHOIR_INACTIVE`, `CHOIR_NOT_SERVING_EVENT`, `LOOKUP_INACTIVE`, `REHEARSAL_TIME_CONFLICT`, `SONG_INACTIVE` |
| GET | `/api/rehearsals?eventId=&fromDate=&toDate=&pageNumber=&pageSize=` · `ChoirDirector` | — | 200 `PagedList<RehearsalDto>` | 400 `VALIDATION_FAILED` (`REPORT_DATE_RANGE_INVALID`) |
| PUT | `/api/rehearsals/{id}` · `ChoirDirector` | `{ "eventId?", "locationId?", "startTime", "endTime", "note?" }` | 200 `RehearsalDto` | 400 `VALIDATION_FAILED` (`REHEARSAL_TIME_INVALID`) · 404 `REHEARSAL_NOT_FOUND`, `LITURGICAL_EVENT_NOT_FOUND`, `LOOKUP_NOT_FOUND` · 409 `REHEARSAL_ALREADY_PASSED`, `LITURGICAL_EVENT_CANCELLED`, `LOOKUP_INACTIVE`, `REHEARSAL_TIME_CONFLICT`, `CHOIR_INACTIVE`, `CHOIR_NOT_SERVING_EVENT` (đổi sự kiện) |
| DELETE | `/api/rehearsals/{id}` · `ChoirDirector` | — | 204 | 404 `REHEARSAL_NOT_FOUND` · 409 `REHEARSAL_HAS_ATTENDANCE` |
| GET | `/api/rehearsals/default-songs?eventId=` · `ChoirDirector` | — | 200 `RehearsalSongDto[]` | 404 `LITURGICAL_EVENT_NOT_FOUND` |

- `eventId` tuỳ chọn: có thì buổi tập là buổi chuẩn bị cho sự kiện đó (sự kiện đã huỷ → 409 `LITURGICAL_EVENT_CANCELLED`);
  bỏ trống là buổi tập thường. `locationId` tuỳ chọn, lấy từ `GET /api/lookups/worship-locations`.
- `endTime` phải sau `startTime` (`REHEARSAL_TIME_INVALID`). `note` tối đa 500 ký tự.
- 409 `REHEARSAL_TIME_CONFLICT`: nơi tập đã có buổi tập khác trùng giờ. Không chọn `locationId` thì không kiểm trùng.
- `songs` bắt buộc gửi (có thể `[]`), không trùng `songId`; thứ tự trong mảng là thứ tự hiển thị — server tự đánh
  `displayOrder` từ 1. `note` của từng bài tối đa 500 ký tự.
- `GET`: mọi buổi tập — đã qua, đang diễn ra, sắp tới — giờ bắt đầu muộn nhất trước. `fromDate` / `toDate` là ngày theo giờ
  Việt Nam, tính cả hai đầu, so với `startTime`. Dùng để chọn buổi tập cần điểm danh.
- `PUT {id}` sửa giờ, nơi tập, sự kiện, ghi chú; **bài hát sửa riêng** qua `PUT {id}/songs`. Buổi tập đã kết thúc →
  `REHEARSAL_ALREADY_PASSED`. Kiểm trùng giờ bỏ qua chính buổi đang sửa. Giữ nguyên nơi tập đã ngưng hoạt động thì được,
  đổi sang nơi tập đã ngưng → `LOOKUP_INACTIVE`. Sửa xong, mọi ca viên đang hoạt động nhận thông báo `RehearsalChanged`
  (`referenceType = "Rehearsal"`).
- `DELETE {id}`: buổi tập đã có điểm danh → `REHEARSAL_HAS_ATTENDANCE` (xoá sẽ mất điểm danh). Xoá buổi chưa kết thúc thì
  mọi ca viên đang hoạt động nhận thông báo `RehearsalChanged` (`referenceType = "LiturgicalEvent"` nếu buổi tập gắn
  sự kiện, ngược lại `null`).
- `GET default-songs` chỉ **gợi ý**, không tạo gì: trả các bài của danh sách bài hát **đã duyệt** của sự kiện, mỗi bài một
  lần theo vị trí xuất hiện đầu tiên, `note` = `null`. Sự kiện chưa có danh sách đã duyệt → `[]`. Ca trưởng chỉnh rồi gửi
  qua `songs` của `POST` hoặc `PUT /api/rehearsals/{id}/songs`.

### `RehearsalAttendanceDto`

```json
{ "memberId": "guid", "fullName": "Nguyễn Văn A", "status": "Present", "checkedAt": "2026-10-09T12:00:00Z" }
```

`status`: `Present` | `Absent` | `Late` | `Excused`, hoặc `null` khi chưa điểm danh (khi đó `checkedAt` cũng `null`).

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/rehearsals/{id}/attendances` | — | 200 `RehearsalAttendanceDto[]` | 404 `REHEARSAL_NOT_FOUND` |
| PUT | `/api/rehearsals/{id}/attendances` | `{ "items": [{ "memberId", "status" }] }` | 204 | 400 `VALIDATION_FAILED` (`REHEARSAL_ATTENDANCE_MEMBER_DUPLICATE`) · 404 `REHEARSAL_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `REHEARSAL_NOT_STARTED`, `MEMBER_NOT_ACTIVE`, `REHEARSAL_ATTENDANCE_ALREADY_RECORDED` |

- Buổi chuẩn bị cho sự kiện cũng là một buổi tập (`Rehearsal` có gắn sự kiện), điểm danh giống hệt.
- `GET` trả mọi ca viên đang hoạt động, cộng những người đã được điểm danh ở buổi này nhưng nay không còn
  hoạt động; sắp theo tên. Không phân trang.
- `PUT` ghi hoặc sửa điểm danh cho từng ca viên trong `items`; ca viên không có trong `items` giữ nguyên.
  Ghi lại `checkedAt` = lúc gọi và người điểm danh là ca trưởng đang gọi.
- Chỉ điểm danh được từ giờ bắt đầu buổi tập trở đi (sửa sau khi kết thúc vẫn được); trước đó → 409 `REHEARSAL_NOT_STARTED`.
- Ca viên trong `items` phải đang hoạt động; mọi lỗi đều chặn cả lô, không ghi dòng nào.
- 409 `REHEARSAL_ATTENDANCE_ALREADY_RECORDED`: ca trưởng khác vừa điểm danh cùng ca viên — tải lại danh sách rồi gửi lại.

### Bài hát của buổi tập — `RehearsalSongDto`

```json
{ "songId": "guid", "songTitle": "Kinh Hòa Bình", "displayOrder": 1, "note": "tập kỹ điệp khúc" }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| GET | `/api/rehearsals/{id}/songs` · mọi role | — | 200 `RehearsalSongDto[]` | 404 `REHEARSAL_NOT_FOUND` |
| PUT | `/api/rehearsals/{id}/songs` · `ChoirDirector` | `{ "items": [{ "songId", "note" }] }` | 200 `RehearsalSongDto[]` | 400 `VALIDATION_FAILED` (`REHEARSAL_SONG_DUPLICATE`) · 404 `REHEARSAL_NOT_FOUND`, `SONG_NOT_FOUND` · 409 `SONG_INACTIVE` |

- `PUT` **thay toàn bộ** danh sách. Thứ tự trong `items` là thứ tự hiển thị — server tự đánh `displayOrder` từ 1, client không gửi.
- `items: []` → xoá hết bài của buổi tập. `note` tối đa 500 ký tự, có thể `null`.
- Bài đã ngừng dùng (`isActive = false`) không thêm mới được (`SONG_INACTIVE`), nhưng bài đã có sẵn trong buổi tập thì được giữ lại.
- Khác `SongList`: không có phiên bản, không qua cha xứ duyệt, không gắn vị trí phụng vụ. Sửa được cả sau khi buổi tập đã diễn ra.
- Chưa có bài nào → `[]`.

---

## 7f. Event participations — `api/event-participations` · gửi yêu cầu + theo dõi: `ChoirDirector` (UC-23 / FE-33, FE-34) · phản hồi: `ChoirMember` (UC-05 / FE-06)

### `EventParticipationDto`

```json
{
  "id": "guid", "eventId": "guid", "memberId": "guid", "memberFullName": "Nguyễn Văn A",
  "status": "Invited", "requestedAt": "2026-10-09T12:00:00Z", "respondedAt": null, "note": null
}
```

`status`: `Invited` | `Confirmed` | `Declined` | `Unsure`. `respondedAt`, `note` là `null` khi ca viên chưa phản hồi.

`EventParticipationSummaryDto` = `{ eventId, totalCount, invitedCount, confirmedCount, declinedCount, unsureCount, items }`,
`items` là `EventParticipationDto[]`.

`MyParticipationDto` = `{ participationId, eventId, status, requestedAt, respondedAt, note, canRespond, cannotRespondReason }`.
`canRespond = true` khi `PUT mine/{eventId}` sẽ nhận câu trả lời lúc này; ngược lại `cannotRespondReason` là mã lỗi `PUT`
sẽ trả (`LITURGICAL_EVENT_CANCELLED`, `MEMBER_NOT_ACTIVE`, `EVENT_PARTICIPATION_ALREADY_RESPONDED`,
`EVENT_PARTICIPATION_CLOSED`). Ca viên `Inactive` / `Left` vẫn thấy lời mời nhưng `canRespond = false`.

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/event-participations` · `ChoirDirector` | `{ "eventId", "memberIds?": ["guid"] }` | 200 `EventParticipationDto[]` | 400 `VALIDATION_FAILED` · 403 `CHOIR_NOT_LEAD` · 404 `LITURGICAL_EVENT_NOT_FOUND`, `MEMBER_NOT_FOUND` · 409 `LITURGICAL_EVENT_CANCELLED`, `LITURGICAL_EVENT_NOT_PUBLISHED`, `LITURGICAL_EVENT_ALREADY_PASSED`, `MEMBER_NOT_ACTIVE`, `EVENT_PARTICIPATION_ALREADY_REQUESTED` |
| GET | `/api/event-participations?eventId=` · `ChoirDirector` | — | 200 `EventParticipationSummaryDto` | 404 `LITURGICAL_EVENT_NOT_FOUND` |
| GET | `/api/event-participations/mine` · `ChoirMember` | — | 200 `MyParticipationDto[]` | 404 `MEMBER_NOT_FOUND` |
| PUT | `/api/event-participations/mine/{eventId}` · `ChoirMember` | `{ "status", "note?" }` | 200 `EventParticipationDto` | 400 `VALIDATION_FAILED` · 404 `MEMBER_NOT_FOUND` · 409 `MEMBER_NOT_ACTIVE`, `EVENT_PARTICIPATION_NOT_INVITED`, `LITURGICAL_EVENT_CANCELLED`, `EVENT_PARTICIPATION_ALREADY_RESPONDED`, `EVENT_PARTICIPATION_CLOSED` |

- `POST` chỉ gửi được cho sự kiện `Published` và chưa diễn ra. Mỗi ca viên được mời nhận một thông báo
  `ParticipationRequest`.
- `memberIds` bỏ trống hoặc `[]` → mời mọi ca viên đang hoạt động **chưa được mời**; gọi lại sau khi có ca viên mới là an
  toàn, không còn ai để mời → `[]`.
- Có `memberIds` → mọi id phải tồn tại, đang hoạt động và chưa được mời; sai một id là chặn cả lô, không tạo dòng nào.
- `POST` trả các yêu cầu **vừa tạo**, không phải toàn bộ yêu cầu của sự kiện.
- `GET` trả mọi yêu cầu của sự kiện kèm số đếm theo trạng thái, `items` sắp theo tên ca viên; chưa mời ai → các số đếm
  bằng 0, `items: []`. Không phân trang.
- `PUT` — ca viên phản hồi yêu cầu của **chính mình**: `status` là `Confirmed` | `Declined` | `Unsure` (không nhận
  `Invited`), `note` tối đa 300 ký tự. Phản hồi **một lần**, không sửa lại được (`EVENT_PARTICIPATION_ALREADY_RESPONDED`).
- 409 `EVENT_PARTICIPATION_NOT_INVITED`: ca viên chưa được mời tham gia sự kiện này. 409 `EVENT_PARTICIPATION_CLOSED`:
  sự kiện đã bắt đầu (so ngày + giờ sự kiện với giờ Việt Nam).
- `GET mine` — yêu cầu của **chính** ca viên cho các sự kiện `Published` từ hôm nay trở đi, sắp theo ngày rồi giờ;
  sự kiện không được mời thì không có trong mảng. Không phân trang, không có gì → `[]`. Dùng `canRespond` để bật / tắt
  nút trả lời thay vì tự tính.

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

## 9. Lookups — `api/lookups` · đọc: mọi role đã đăng nhập · ghi: `Admin` · `liturgical-slots` ghi thêm: `ChoirDirector` (UC-32 / FE-49, FE-50)

Danh mục cho dropdown. Chỉ trả dòng **đang hoạt động**; Admin tắt một dòng thì nó biến khỏi
danh sách (dữ liệu cũ vẫn trỏ tới nó). Không phân trang — trả mảng.

| Route | Phần tử | Sắp theo |
|---|---|---|
| `GET /api/lookups/mass-types` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/ceremony-types` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/event-categories` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/song-themes` | `{ "id", "name", "description", "isActive", "parentId" }` | tên |
| `GET /api/lookups/skill-categories` | `{ "id", "name", "description", "isActive" }` | tên |
| `GET /api/lookups/liturgical-seasons` | `{ "id", "name", "startDate", "endDate", "colorHex", "isActive" }` | `startDate` |
| `GET /api/lookups/liturgical-slots` | `{ "id", "name", "defaultOrder", "isActive" }` | `defaultOrder` (thứ tự trong lễ) |
| `GET /api/lookups/worship-locations` | `{ "id", "name", "address", "isActive" }` | tên |
| `GET /api/lookups/skills?categoryId=` | `{ "id", "categoryId", "name", "description", "isActive" }` | tên |

- `description`, `address`, `colorHex` có thể `null`.
- `skills`: `categoryId` tuỳ chọn; bỏ qua kỹ năng thuộc nhóm đã tắt.
- `?includeInactive=true` trên `mass-types`, `ceremony-types`, `event-categories`, `skill-categories`,
  `liturgical-seasons`, `skills`, `song-themes`, `liturgical-slots`, `worship-locations`: trả cả dòng đã tắt (kể cả kỹ năng thuộc nhóm đã tắt) — **chỉ Admin**; role khác
  gửi cờ này bị bỏ qua.

### Admin cấu hình danh mục (UC-32 / FE-49, FE-50)

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST · PUT `{id}` | `/api/lookups/mass-types` · `ceremony-types` · `event-categories` · `skill-categories` | `{ "name", "description?", "isActive" }` | 200 phần tử như bảng trên | 400 `VALIDATION_FAILED` · 404 `LOOKUP_NOT_FOUND` (PUT) · 409 `LOOKUP_NAME_DUPLICATE`, `LOOKUP_IN_USE` (chỉ `skill-categories`) |
| POST · PUT `{id}` | `/api/lookups/skills` | `{ "categoryId", "name", "description?", "isActive" }` | 200 `SkillDto` | 400 `VALIDATION_FAILED` · 404 `SKILL_CATEGORY_NOT_FOUND`, `SKILL_NOT_FOUND` (PUT) · 409 `SKILL_NAME_DUPLICATE`, `LOOKUP_INACTIVE`, `SKILL_IN_USE` |
| POST · PUT `{id}` | `/api/lookups/song-themes` | `{ "name", "description?", "isActive", "parentId?" }` | 200 `SongThemeDto` | 400 `VALIDATION_FAILED`, `SONG_THEME_PARENT_INVALID` · 404 `LOOKUP_NOT_FOUND` (PUT) · 409 `LOOKUP_NAME_DUPLICATE` |
| POST · PUT `{id}` | `/api/lookups/liturgical-slots` · `Admin`, `ChoirDirector` | `{ "name", "defaultOrder", "isActive" }` | 200 `LiturgicalSlotDto` | như trên |
| POST · PUT `{id}` | `/api/lookups/worship-locations` | `{ "name", "address?", "isActive" }` | 200 `WorshipLocationDto` | như trên |
| POST · PUT `{id}` | `/api/lookups/liturgical-seasons` | `{ "name", "startDate", "endDate", "colorHex?", "isActive" }` | 200 `LiturgicalSeasonDto` | 400 `VALIDATION_FAILED` (`LITURGICAL_SEASON_DATE_INVALID`) · 404 `LOOKUP_NOT_FOUND` (PUT) · 409 `LITURGICAL_SEASON_DATE_OVERLAP` |

- `isActive` mặc định `true`. **Không có xoá**: các bảng khác đang trỏ tới danh mục, nên muốn bỏ thì gửi
  `PUT` với `isActive: false`. Bật lại bằng `isActive: true`.
- `PUT` gửi đủ mọi trường (thay toàn bộ). `name` được cắt khoảng trắng hai đầu.
- `liturgical-slots`: ca trưởng cũng tạo / sửa được, để thêm mục riêng (vd "Ca nguyện Đức Mẹ") cho danh sách bài hát.
  Mục mới dùng chung cho mọi danh sách sau này. Ca trưởng không đọc được mục đã tắt (`includeInactive` chỉ cho Admin).
- `song-themes`, `liturgical-slots`, `worship-locations`: response giờ có thêm `isActive`. Tên chủ đề tối đa 50 ký tự;
  tên vị trí phụng vụ và nơi cử hành tối đa 100; `address` tối đa 300; `defaultOrder` ≥ 0.
- `GET /api/lookups/liturgical-slots/{id}/requirements` (mọi role) · `PUT` (chỉ Admin, body
  `{ "requirements": [{ "skillId", "requiredCount" }] }`): người vị trí đó luôn cần dù đặt bài nào, ví dụ Đáp ca → 1 người
  xướng. Trả `{ "skillId", "skillName", "skillCategoryId", "requiredCount" }[]`. Lỗi: 400 `VALIDATION_FAILED` · 404
  `LOOKUP_NOT_FOUND`, `SKILL_NOT_FOUND` · 409 `SKILL_INACTIVE`. Chỉ áp cho bài đưa vào vị trí **sau** khi sửa.
- `song-themes` là **cây nhiều cấp** (vd Đức Maria › Mân Côi): `parentId` `null` là gốc. API trả danh sách phẳng, FE tự
  dựng cây theo `parentId`. `SONG_THEME_PARENT_INVALID` khi cha không tồn tại, đã tắt (trừ khi giữ nguyên cha cũ),
  hoặc chính là chủ đề đó / một chủ đề con cháu của nó. Không giới hạn số cấp.
- Chủ đề đang hoạt động không nằm dưới chủ đề đã tắt: tắt chủ đề còn con đang hoạt động → 409 `SONG_THEME_HAS_ACTIVE_CHILDREN` (tắt con
  trước); bật hoặc tạo chủ đề dưới cha đã tắt → 400 `SONG_THEME_PARENT_INVALID`.
- Tắt một mục không gỡ nó khỏi dữ liệu đang dùng: sự kiện, danh sách bài hát cũ vẫn giữ tham chiếu; mục đã tắt
  chỉ không chọn được cho bản ghi mới.
- `name`: bắt buộc, tối đa 50 ký tự (mùa phụng vụ: 100); `description` tối đa 300; trùng tên (không phân biệt
  hoa thường) với dòng khác → 409. Tên kỹ năng chỉ cần duy nhất **trong nhóm**.
- Mùa phụng vụ: `endDate` phải sau `startDate`, nếu không → `LITURGICAL_SEASON_DATE_INVALID`; `colorHex` dạng `#RRGGBB`.
  Tên mùa được trùng (mỗi năm một dòng), nhưng một mùa đang hoạt động không được trùng khoảng ngày với mùa
  đang hoạt động khác → 409 `LITURGICAL_SEASON_DATE_OVERLAP`.
- **5 nhóm kỹ năng cố định không tắt được**: `PUT skill-categories/{id}` với `isActive: false` trên một trong 5 nhóm
  dưới đây → 409 `LOOKUP_IN_USE`. Đổi tên, mô tả vẫn được. Nhóm do Admin tự tạo thì tắt bình thường.
- **Không đưa kỹ năng vào nhóm đã tắt**: tạo kỹ năng, hoặc chuyển kỹ năng sang một nhóm có `isActive: false`
  → 409 `LOOKUP_INACTIVE`. Sửa tên / mô tả của kỹ năng vốn đã nằm trong nhóm tắt vẫn được.
- **Chuyển nhóm của kỹ năng đang được bài hát dùng**: nếu việc chuyển đưa kỹ năng vào hoặc ra khỏi nhóm
  `Instrument` trong khi kỹ năng đang nằm trong yêu cầu bè / nhạc cụ của một bài hát → 409 `SKILL_IN_USE`.
  Chuyển giữa các nhóm không phải nhạc cụ, hoặc chuyển kỹ năng chưa bài nào dùng, vẫn được.
- Id của 5 nhóm kỹ năng là cố định trên mọi môi trường:

| Nhóm | Id |
|---|---|
| Vocal | `c4829abd-bb1d-4c6c-b401-9a177a88e66a` |
| Instrument | `0904ad8b-87f2-46c5-9938-f6cfbf0fa70b` |
| Solo | `550a67bb-0393-4bc4-8fe0-d7a453871f81` |
| Psalm | `a1c322c5-14af-4ca4-892a-b6e8d0eacbbd` |
| Conducting support | `e2722720-3745-48ea-9e8e-14ec7a63907d` |

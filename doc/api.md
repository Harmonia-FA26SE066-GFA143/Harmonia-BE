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
  "user": { "id": "guid", "email": "a@b.com", "roleName": "ChoirMember" }
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
{ "id": "guid", "email": "a@b.com", "roleName": "ChoirDirector", "isActive": true }
```

| Method | Route | Body | Thành công | Lỗi |
|---|---|---|---|---|
| POST | `/api/users` | `{ "email", "password", "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `ROLE_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PUT | `/api/users/{id}` | `{ "email" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND` · 409 `USER_EMAIL_ALREADY_EXISTS` |
| PATCH | `/api/users/{id}/activate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_ALREADY_ACTIVE` |
| PATCH | `/api/users/{id}/deactivate` | — | 204 | 404 `USER_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_ALREADY_INACTIVE`, `USER_LAST_ADMIN` |
| PUT | `/api/users/{id}/role` | `{ "roleName" }` | 200 `UserDto` | 400 `VALIDATION_FAILED` · 404 `USER_NOT_FOUND`, `ROLE_NOT_FOUND` · 409 `USER_CANNOT_MODIFY_SELF`, `USER_LAST_ADMIN` |

`roleName` phải là một trong 4 role. Lỗi validate của nhóm này hiện chỉ trả
`VALIDATION_FAILED` trong `errors`, chưa có mã riêng theo field.

---

## 4. Member profiles — `api/member-profiles` · role `ChoirMember`

### `GET /api/member-profiles/me`

Hồ sơ của chính người gọi.

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

`200` · `404 MEMBER_NOT_FOUND` (tài khoản chưa có hồ sơ).

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

## 6. SignalR — `/hubs/notifications`

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

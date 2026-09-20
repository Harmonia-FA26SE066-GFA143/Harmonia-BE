# Bảo mật — không có ngoại lệ

## Secret

- `appsettings.json` chỉ chứa config KHÔNG nhạy cảm. Không connection string thật, không JWT key.
- Secret dev nằm trong `src/Harmonia.API/.env` (đã gitignore).
  `Program.cs` gọi `Env.Load()` ở dòng đầu tiên.
- Thêm key mới thì PHẢI thêm key rỗng tương ứng vào `.env.example`.
- Không hardcode secret trong code, không in ra log, không đọc ra chat, không commit.

## Tài khoản & mật khẩu

- Tài khoản do `Admin` tạo và gán role. **KHÔNG có đăng ký công khai** —
  chỉ `login` và `forgot-password` là `[AllowAnonymous]`.
- Mật khẩu lưu dưới dạng hash + salt (ASP.NET Core `PasswordHasher<T>` hoặc BCrypt).
  Không bao giờ lưu plaintext, không bao giờ trả mật khẩu ra response.
- Token reset mật khẩu: ngẫu nhiên đủ dài, hết hạn trong vòng 1 giờ, chỉ dùng được một lần.
- Thông báo lỗi đăng nhập luôn chung chung ("sai tài khoản hoặc mật khẩu"),
  không tiết lộ email có tồn tại hay không.

## JWT

- Khóa ký lấy từ `.env`, tối thiểu 256 bit. Không commit, không đặt trong `appsettings.json`.
- Luôn validate `Issuer`, `Audience`, `Lifetime`, `SigningKey`. Không tắt cái nào cho "tiện test".
- Access token ngắn hạn (15–60 phút). Cần phiên dài thì dùng refresh token, không kéo dài access token.
- Claim bắt buộc: user id, role, `ChoirId`.

## Phân quyền

- Mọi endpoint PHẢI có `[Authorize(Roles = ...)]`.
- Role hệ thống: `ParishPriest`, `ChoirDirector`, `ChoirMember`, `Admin`.
- Chỉ `ChoirDirector` duyệt kỹ năng. Chỉ `ParishPriest` duyệt `SongList`.
  Chỉ `Admin` đụng tài khoản và danh mục hệ thống.
- **Kiểm quyền hai tầng**: attribute chặn theo role, service chặn theo quyền sở hữu.
  `ICurrentUserService` cho biết ai đang gọi.
- Luôn kiểm dữ liệu có thuộc `Choir` của người gọi không trước khi đọc/sửa.
  `ChoirDirector` của ca đoàn A không được đụng dữ liệu ca đoàn B — kể cả khi đúng role.
  Sai id trả `404`, không trả `403`, để không lộ sự tồn tại của bản ghi.

## SignalR

- `NotificationHub` phải có `[Authorize]`. Hub KHÔNG tự bảo vệ.
- Gửi notification theo user hoặc group, KHÔNG broadcast `Clients.All`.
- Cấu hình đọc JWT từ query string chỉ áp cho đúng path của Hub
  (`context.Request.Path.StartsWithSegments("/hubs/...")`), không bật toàn cục.

## Upload file

- Whitelist phần mở rộng: bản nhạc (`.pdf`, `.png`, `.jpg`), audio (`.mp3`, `.m4a`, `.wav`).
  Chặn theo whitelist, không chặn theo blacklist.
- Không tin `Content-Type` client gửi lên — kiểm phần mở rộng lẫn dung lượng ở server.
- Giới hạn dung lượng rõ ràng cho từng loại.
- Lưu bằng tên sinh mới (GUID), không dùng tên file gốc — tránh path traversal và ghi đè.
- Không phục vụ file trực tiếp từ thư mục upload; trả URL có kiểm quyền.

## Dữ liệu

- Không trả entity thẳng ra API, luôn qua DTO — tránh lộ field nội bộ.
- Không nhận entity làm tham số action, luôn qua `Request` — tránh over-posting.
- Không dùng `FromSqlRaw` với chuỗi nối. Cần SQL thô thì truyền tham số.
- Không log thông tin cá nhân thành viên (số điện thoại, email) ở mức Information.
- `GlobalExceptionHandlingMiddleware` không được trả stack trace hay message gốc
  của exception ra client ở môi trường Production.

## CORS

- Liệt kê origin cụ thể của web và mobile. KHÔNG dùng `AllowAnyOrigin()`.
- SignalR cần `AllowCredentials()`, mà `AllowAnyOrigin()` + `AllowCredentials()` là
  cấu hình sai — ASP.NET Core sẽ ném lỗi lúc chạy.
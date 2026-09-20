# Bảo mật — không có ngoại lệ

## Secret

- `appsettings.json` chỉ chứa config KHÔNG nhạy cảm. Không connection string thật, không JWT key.
- Secret dev nằm trong `src/Harmonia.API/.env` (đã gitignore). Production KHÔNG có file
  `.env` — giá trị đặt ở App Settings của App Service.
- **Tên key dùng cú pháp lồng cấp của ASP.NET Core**: hai gạch dưới `__` tương đương dấu
  hai chấm. `Jwt__Key` đọc ra bằng `configuration["Jwt:Key"]`,
  `ConnectionStrings__DefaultConnection` đọc bằng
  `configuration.GetConnectionString("DefaultConnection")`.
  Nhờ vậy cùng một tên key chạy được cả ở `.env` lẫn App Settings trên Azure — không phải
  viết hai nhánh code cho hai môi trường.
- **Thứ tự nạp trong `Program.cs`, không được đảo:**

  ```csharp
  var builder = WebApplication.CreateBuilder(args);
  DotNetEnv.Env.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));
  builder.Configuration.AddEnvironmentVariables();
  ```

  Hai chỗ dễ sai:
  - Phải truyền đường dẫn dựng từ `ContentRootPath`. `Env.Load()` không tham số đi tìm
    `.env` ở thư mục làm việc, mà `dotnet ef` chạy từ gốc solution nên sẽ không thấy file
    — hậu quả là lệnh migration chết với lỗi thiếu connection string.
  - Phải gọi lại `AddEnvironmentVariables()` SAU `Env.Load`. `CreateBuilder` đã đọc xong
    biến môi trường trước khi `.env` kịp nạp, bỏ dòng này thì `IConfiguration` không có
    key nào của `.env`.
- Đọc secret qua `IConfiguration`, không rải `Environment.GetEnvironmentVariable` khắp nơi.
  Thiếu key thì ném ngay lúc khởi động kèm tên key trong message — đừng để `null` trôi
  xuống rồi vỡ ở chỗ khác với lỗi khó hiểu.
- Thêm key mới thì PHẢI thêm key rỗng tương ứng vào `.env.example`.
- Không hardcode secret trong code, không in ra log, không đọc ra chat, không commit.

## Tài khoản & mật khẩu

- Tài khoản do `Admin` tạo và gán role. **KHÔNG có đăng ký công khai** —
  chỉ `login` và `forgot-password` là `[AllowAnonymous]`.
- Mật khẩu lưu ở `User.passwordHash`, dùng `PasswordHasher<T>` hoặc BCrypt.
  Không bao giờ lưu plaintext, không bao giờ trả mật khẩu ra response.
- Token reset mật khẩu: ngẫu nhiên đủ dài, hết hạn trong 1 giờ, dùng một lần.
- `forgot-password` luôn trả 200 dù email có tồn tại hay không — không tiết lộ
  email nào đã đăng ký. Không có mã lỗi cho trường hợp này.
- Đăng nhập sai luôn trả `AUTH_INVALID_CREDENTIALS`, không phân biệt sai email hay sai mật khẩu.

## JWT & RefreshToken

- Khóa ký lấy từ `.env`, tối thiểu 256 bit. Luôn validate `Issuer`, `Audience`,
  `Lifetime`, `SigningKey` — không tắt cái nào cho "tiện test".
- Access token 15–60 phút, stateless. Claim bắt buộc: user id, role.
- `RefreshToken` lưu DB ở cột `TokenHash` — **lưu hash, không lưu token gốc**.
  Token gốc chỉ xuất hiện một lần trong response trả về client, không ghi log, không lưu lại.
  Tra cứu bằng cách hash token client gửi lên rồi so với `TokenHash`.
- Mỗi lần refresh phải xoay vòng: thu hồi token cũ (`revokedAt`), phát token mới.
- Logout = đánh dấu `revokedAt`, không phải chỉ xoá ở client.
- Đăng xuất mọi thiết bị = thu hồi toàn bộ `RefreshToken` của user đó.

## Phân quyền

- Mọi endpoint PHẢI có `[Authorize(Roles = ...)]`.
- **4 role**: `Admin`, `ParishPriest`, `ChoirDirector`, `ChoirMember`.
  Không có role `Instrumentalist` — nhạc công là `ChoirMember` có `MemberSkill`
  thuộc `SkillCategory` = Instrument.
- Chỉ `ChoirDirector` duyệt `MemberSkill`, tạo `Rehearsal`, chốt `ServiceRoster`.
  Chỉ `ParishPriest` tạo `SongListReview`. Chỉ `Admin` đụng `User`, `Role`,
  `SystemSetting` và 9 bảng lookup.
- **Kiểm quyền hai tầng**: attribute chặn theo role, service chặn theo quyền sở hữu bản ghi.
  `ICurrentUserService` cho biết ai đang gọi.
- Ca viên chỉ đọc/sửa bản ghi **của chính mình**: `MemberSkill`, `EventParticipation`,
  `PracticeSubmission`, `MaterialLearningProgress`, `MemberProfile`.
  Truy cập bản ghi của người khác → trả **404**, không trả 403, để không lộ sự tồn tại.
- Ca viên chỉ xem được `SongList` ở trạng thái `Approved`, và `LiturgicalWeek`
  ở trạng thái `Published`.

## SignalR

- `NotificationHub` phải có `[Authorize]`. Hub KHÔNG tự bảo vệ.
- Gửi theo user hoặc group, KHÔNG broadcast `Clients.All`.
- Cấu hình đọc JWT từ query string chỉ áp cho đúng path của Hub
  (`context.Request.Path.StartsWithSegments("/hubs/...")`), không bật toàn cục.

## Upload file

- Whitelist phần mở rộng: `MusicMaterial` nhận `.pdf`, `.png`, `.jpg`;
  `PracticeSubmission` nhận `.mp3`, `.m4a`, `.wav`. Chặn theo whitelist, không blacklist.
- Không tin `Content-Type` client gửi — kiểm phần mở rộng lẫn dung lượng ở server.
- Lưu bằng tên sinh mới (GUID), không dùng tên gốc — tránh path traversal và ghi đè.
  `fileName` gốc chỉ để hiển thị.
- Không phục vụ file trực tiếp từ thư mục upload; trả URL có kiểm quyền.

## Dữ liệu

- Không trả entity thẳng ra API, luôn qua DTO.
- Không nhận entity làm tham số action, luôn qua `Request` — tránh over-posting.
- Không dùng `FromSqlRaw` với chuỗi nối.
- Không log `phone`, `email`, `dateOfBirth` của ca viên ở mức Information.
- `AuditLog.oldValue` / `newValue` không được chứa `passwordHash` hay token.
- `GlobalExceptionHandlingMiddleware` không trả stack trace hay message gốc của
  exception ra client ở Production — chỉ trả `INTERNAL_ERROR`.

## CORS

- Liệt kê origin cụ thể của web và mobile. KHÔNG dùng `AllowAnyOrigin()`.
- SignalR cần `AllowCredentials()`, mà `AllowAnyOrigin()` + `AllowCredentials()`
  là cấu hình sai — ASP.NET Core ném lỗi lúc chạy.
<div align="center">

# 🎼 Harmonia — Backend

**Hệ thống quản lý điều phối ca đoàn & Thánh nhạc phụng vụ**

*Để ca trưởng bớt nhắn Zalo lúc 11 giờ đêm, và cha xứ duyệt bài hát chỉ bằng một cú bấm.*

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core%209-CC2927?logo=microsoftsqlserver&logoColor=white)
![SignalR](https://img.shields.io/badge/realtime-SignalR-0078D4)
![Azure](https://img.shields.io/badge/deploy-Azure%20App%20Service-0089D6?logo=microsoftazure&logoColor=white)
![Tests](https://img.shields.io/badge/tests-xUnit%20v3-5C2D91)

Đồ án capstone **FA26SE066** · 09/2026 – 03/2027

</div>

---

## ✨ Harmonia làm gì?

Một ca đoàn giáo xứ mỗi tuần phải trả lời hàng loạt câu hỏi: *Chúa nhật này lễ gì? Hát bài nào? Ai giữ đàn organ? Ai xướng Đáp ca? Tối thứ Năm tập có đủ bè Tenor không?* Harmonia gom tất cả vào một chỗ.

| | Tính năng | Điểm nhấn |
|---|---|---|
| 📅 | **Lịch phụng vụ theo ngày** | Đồng bộ từ Liturgical Calendar API, dịch tên lễ sang tiếng Việt, sinh sự kiện từ giờ lễ cố định |
| 🎵 | **Thư viện thánh ca** | Bản nhạc, lời, audio mẫu, MIDI → render audio, lời Karaoke có mốc thời gian, Gemini trích lời từ bản nhạc |
| 📝 | **Danh sách bài hát** | Ca trưởng đề xuất theo vị trí (Nhập lễ, Đáp ca, Hiệp lễ…), cha xứ duyệt / từ chối, mỗi lần sửa là một phiên bản |
| 👥 | **Thành viên & kỹ năng** | Ca viên khai báo bè / nhạc cụ kèm bản thu minh chứng, ca trưởng duyệt |
| 🧩 | **Phân công phục vụ** | Gợi ý phân công bằng AI (Gemini) có luật dự phòng, cảnh báo thiếu người tính lúc chạy |
| 🎤 | **Bài tập & chấm điểm** | Ca viên thu âm nộp bài, server chấm cao độ (YIN + DTW) so với MIDI, ca trưởng nhận xét |
| ✅ | **Buổi tập & điểm danh** | Lịch tập, bài tập trong buổi, bản thu phòng tập, xác nhận tham gia sự kiện |
| 🔔 | **Thông báo realtime** | SignalR hub `/hubs/notifications`, nhắc xác nhận tham gia hằng ngày |
| 📊 | **Báo cáo** | Xuất CSV / XLSX / PDF, nhật ký hoạt động (audit log) |
| ⛪ | **Nhiều ca đoàn** | Mỗi ca đoàn một ca trưởng, sự kiện nhiều ca đoàn có ca đoàn phụ trách |

Chi tiết đủ 15 feature: [docs/harmonia-feature-list.md](docs/harmonia-feature-list.md).

## 🎭 Bốn vai trò

```text
   ⛪ ParishPriest            🎼 ChoirDirector             🎙️ ChoirMember            🛠️ Admin
   (Web)                      (Web)                        (Mobile)                  (Web)
   duyệt danh sách bài        quản lý ca viên, kỹ năng     khai báo kỹ năng          tài khoản, danh mục,
   khai báo sự kiện           đề xuất bài, phân công       xác nhận tham gia         cấu hình, báo cáo
   chọn ca đoàn phụ trách     lên lịch tập, chấm bài       nộp bản thu tập hát
```

> Nhạc công **không** có role riêng — họ là `ChoirMember` có kỹ năng thuộc nhóm nhạc cụ.

## 🏛️ Kiến trúc

Clean Architecture bốn tầng, phụ thuộc chỉ đi vào trong:

```text
┌─────────────────────────────────────────────────────────────┐
│  Harmonia.API            Controllers · Hubs · Middlewares   │
│                          Filters · Extensions (JWT, CORS…)  │
├─────────────────────────────────────────────────────────────┤
│  Harmonia.Infrastructure EF Core · Repositories · Cloudinary│
│                          Brevo · Gemini · MIDI · Workers    │
├─────────────────────────────────────────────────────────────┤
│  Harmonia.Application    Services · DTOs · Validators       │
│                          AutoMapper · Result<T>             │
├─────────────────────────────────────────────────────────────┤
│  Harmonia.Domain         56 entity · 29 enum · ErrorCodes   │
│                          (không phụ thuộc gì)               │
└─────────────────────────────────────────────────────────────┘
```

- **Repository Pattern** — repository trả entity, service trả DTO.
- **`Result<T>`** cho lỗi nghiệp vụ, mang mã lỗi `SCREAMING_SNAKE` ([docs/error-codes.md](docs/error-codes.md)) — FE tự dịch.
- **Kiểm quyền hai tầng** — attribute chặn theo role, service chặn theo phạm vi ca đoàn (`IChoirScopeService`). Bản ghi ngoài phạm vi trả **404**, không lộ sự tồn tại.

## 🧰 Công nghệ

| Mảng | Dùng gì |
|---|---|
| Nền tảng | .NET 9, ASP.NET Core Web API, Swagger |
| Dữ liệu | EF Core 9 + SQL Server (Azure SQL ở production) |
| Xác thực | JWT + refresh token xoay vòng (lưu hash), Google Sign-In |
| Realtime | SignalR (Azure SignalR Service ở production) |
| File | Cloudinary (private + URL ký có hạn) |
| Email | Brevo |
| AI | Gemini — gợi ý phân công, trích lời bài hát |
| Âm nhạc | DryWetMidi, MeltySynth (render MIDI), NAudio, TagLib# |
| Báo cáo | ClosedXML (XLSX), QuestPDF (PDF) |
| Kiểm thử | xUnit v3, NSubstitute, `WebApplicationFactory`, SQLite in-memory |

## 🚀 Chạy thử trong 5 phút

**Cần có:** .NET 9 SDK · SQL Server (local hoặc Azure)

```bash
# 1. Lấy code
git clone <repo-url> && cd Harmonia.Solution

# 2. Tạo file secret từ mẫu rồi điền giá trị thật
cp src/Harmonia.API/.env.example src/Harmonia.API/.env

# 3. Tạo database
dotnet ef database update --project src/Harmonia.Infrastructure --startup-project src/Harmonia.API

# 4. Chạy
dotnet run --project src/Harmonia.API                          # http://localhost:5259
dotnet run --project src/Harmonia.API --launch-profile https   # https://localhost:7112
```

Mở **`/swagger`** để thử API (trình duyệt không tự bật).

> 🔑 **Admin đầu tiên** được tạo lúc khởi động từ `Seed__AdminEmail` / `Seed__AdminPassword` khi DB chưa có Admin nào. Đăng nhập xong nhớ đổi mật khẩu rồi xoá `Seed__AdminPassword` khỏi `.env`.

### Biến môi trường

Toàn bộ nằm trong [src/Harmonia.API/.env.example](src/Harmonia.API/.env.example). Tên key dùng `__` thay cho `:` nên chạy được cả ở `.env` lẫn App Settings của Azure.

| Nhóm | Key | Bắt buộc |
|---|---|---|
| Database | `ConnectionStrings__DefaultConnection` | ✅ |
| JWT | `Jwt__Key` (≥ 32 ký tự), `Jwt__Issuer`, `Jwt__Audience`, … | ✅ |
| CORS | `Cors__AllowedOrigins` | ✅ |
| File | `Cloudinary__*` | ✅ |
| Email | `Brevo__*`, `PasswordReset__WebUrl`, `PasswordReset__MobileUrl` | ✅ |
| Google | `Google__ClientIds` | khi dùng Google Sign-In |
| SignalR | `Azure__SignalR__ConnectionString` | để trống ở Development |
| AI | `Gemini__ApiKey`, `Gemini__Model` | lỗi thì rơi về luật |

> ⚠️ Không bao giờ commit `.env`. `appsettings.json` chỉ chứa cấu hình không nhạy cảm.

## 🧪 Kiểm thử

```bash
dotnet build Harmonia.Solution.slnx
dotnet test
```

Hơn **1.100 test** chia bốn project khớp bốn tầng: `Domain.Tests`, `Application.Tests`, `Infrastructure.Tests`, `API.Tests` (integration qua `WebApplicationFactory`).

## 📁 Cấu trúc thư mục

```text
Harmonia.Solution/
├── src/
│   ├── Harmonia.Domain/          # Entities, Enums, Exceptions, ValueObjects, Common/ErrorCodes
│   ├── Harmonia.Application/     # Services, DTOs, Validators, Mappings, Interfaces
│   ├── Harmonia.Infrastructure/  # Data (DbContext, Configurations, Migrations), Repositories,
│   │                             # ExternalServices, BackgroundJobs
│   └── Harmonia.API/             # Controllers, Hubs, Middlewares, Filters, Extensions
├── test/                         # 4 project test, mỗi tầng một
└── docs/                         # Tài liệu nghiệp vụ, API, mã lỗi, sơ đồ
```

## 📚 Tài liệu

| File | Nội dung |
|---|---|
| [docs/api.md](docs/api.md) | Danh sách endpoint |
| [docs/api-changelog.md](docs/api-changelog.md) | Lịch sử thay đổi API cho FE |
| [docs/error-codes.md](docs/error-codes.md) | Mã lỗi và bản dịch |
| [docs/harmonia-domain-entity-list.md](docs/harmonia-domain-entity-list.md) | Nguồn chuẩn entity + enum |
| [docs/harmonia-quy-trinh-nghiep-vu.md](docs/harmonia-quy-trinh-nghiep-vu.md) | Quy trình nghiệp vụ |
| [docs/harmonia-use-case-descriptions.md](docs/harmonia-use-case-descriptions.md) | Mô tả use case |

## 🤝 Quy ước

- Code, comment, log, exception viết **tiếng Anh**; thông báo và email cho người dùng viết tiếng Việt.
- Thuật ngữ nghiệp vụ dùng đúng tên trong từ điển ([.claude/rules/02-naming.md](.claude/rules/02-naming.md)) — `ChoirDirector`, không phải `ChoirLeader`; `LiturgicalEvent`, không phải `Event`.
- Mọi endpoint có `[Authorize]`; endpoint ẩn danh mới phải có rate limit.
- Thêm mã lỗi mới → cập nhật [docs/error-codes.md](docs/error-codes.md) trong cùng PR.
- Commit theo Conventional Commits: `feat(song-list): ...`, `fix(roster): ...`.

---

<div align="center">

*Soli Deo Gloria* 🕊️

</div>

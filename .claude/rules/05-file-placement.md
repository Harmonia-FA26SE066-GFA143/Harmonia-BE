  # Đặt file ở đâu

  Chỉ dùng các thư mục trong bảng dưới. **Không tạo thư mục mới** — cần chỗ mới thì hỏi.
  Trước khi tạo file, xác định nó thuộc dòng nào; không khớp dòng nào thì hỏi.

  ## Harmonia.Domain — không phụ thuộc gì

  | Thư mục | Chứa | KHÔNG chứa |
  |---|---|---|
  | `Common/` | Lớp cơ sở dùng chung (base entity, value object gốc), hằng tên role (`RoleNames.ChoirDirector`), `ErrorCodes` | Entity cụ thể, câu hiển thị, HTTP status |
  | `Entities/` | Entity nghiệp vụ | Data annotation, attribute EF |
  | `Enums/` | Enum | Enum chỉ dùng cho API response |
  | `Exceptions/` | `DomainException` và exception con | Exception kiểu NotFound, Validation |
  | `ValueObjects/` | Đối tượng so sánh theo giá trị, không có Id | Entity |

  ## Harmonia.Application — chỉ phụ thuộc Domain

  | Thư mục | Chứa | KHÔNG chứa |
  |---|---|---|
  | `Common/` (gốc) | — | Mọi file |
  | `Common/Models/` | `Result`, `Result<T>`, `PagedList<T>`, `PagingRequest`, class Options mà service Application đọc (`PasswordResetOptions`) — bind và validate ở `Infrastructure/DependencyInjection.cs` | DTO nghiệp vụ, interface, code EF Core |
  | `DTOs/` | `Dto`, `Request`, `Response` | Entity |
  | `Exceptions/` | Exception ứng dụng: NotFound, Validation, Unauthorized | Exception nghiệp vụ (→ Domain) |
  | `Interfaces/IRepositories/` | Interface truy cập dữ liệu | Code EF Core |
  | `Interfaces/IServices/` | Interface service nghiệp vụ **và** interface cho dịch vụ ngoài (`IEmailSender`, `IJwtTokenService`) | Implementation |
  | `Mappings/` | AutoMapper profile | Logic nghiệp vụ |
  | `Services/` | Service nghiệp vụ thuần | `using` EF Core, SignalR, HttpClient |
  | `Validators/` | FluentValidation validator | Kiểm tra cần truy vấn DB phức tạp (→ `Services/`) |

  ## Harmonia.Infrastructure — implement interface của Application

  | Thư mục | Chứa | KHÔNG chứa |
  |---|---|---|
  | `Data/` | `HarmoniaDbContext`, `DataSeeder` (role + Admin đầu tiên, dữ liệu demo), extension truy vấn EF (`ToPagedListAsync`) | Repository |
  | `Data/Configurations/` | `IEntityTypeConfiguration<T>`, seed cố định qua `HasData()` (lookup) | Dữ liệu demo, role |
  | `Data/Interceptors/` | EF interceptor (audit, …) | Logic nghiệp vụ |
  | `Migrations/` | Migration EF sinh ra | Bất cứ file nào viết tay — không tạo, không sửa |
  | `Repositories/` | Implementation của `IRepositories` | Logic nghiệp vụ |
  | `ExternalServices/` | Implementation chạm công nghệ ngoài: email, **phát** JWT, file storage, Gemini — **kèm class Options của chính nó** (`JwtOptions` nằm cạnh `JwtTokenService`) | Logic nghiệp vụ |

  **Role KHÔNG seed bằng `HasData()`** (chốt 2026-10-03): DB Azure đã có 4 role với Id ngẫu nhiên,
  migration `InsertData` sẽ vấp unique index `Name`. `DataSeeder` chèn role theo **tên** lúc khởi
  động, chỉ thêm, không sửa/xoá. Admin đầu tiên tạo từ `Seed__AdminEmail` / `Seed__AdminPassword`
  khi DB chưa có Admin nào đang hoạt động.

  ## Harmonia.API — trình diễn và composition root

  | Thư mục | Chứa | KHÔNG chứa |
  |---|---|---|
  | `Controllers/` | Controller | Logic nghiệp vụ, `DbContext`, repository |
  | `Hubs/` | SignalR hub + interface client | Logic nghiệp vụ |
  | `Services/` | Implementation cần thứ chỉ API có (`IHubContext`) | Service nghiệp vụ (→ Application) |
  | `Middlewares/` | Middleware pipeline, bảng mã lỗi → HTTP status | Cấu hình DI |
  | `Extensions/` | Cấu hình DI và pipeline đặc thù API (Swagger, **xác thực** JWT, CORS, SignalR), **và** extension chỉ API dùng (`ClaimsPrincipal.GetUserId()`) | Logic nghiệp vụ |
  | `Filters/` | Action / authorization filter | Middleware |

  `Extensions/` đặt tên `<Chủ đề>Extensions.cs`, method `AddApi<X>` / `UseApi<X>` —
  tiền tố `Api` để không đụng tên extension có sẵn của thư viện (`AddCors`, `AddSwagger`).

  ## Program.cs chỉ gọi, không cấu hình

  `Program.cs` là danh sách lời gọi `AddX()` / `UseX()`, không chứa khối lambda cấu hình.
  Thấy `builder.Services.AddX(o => { ... })` trong `Program.cs` là sai chỗ → chuyển vào `Extensions/`.

  ## Hai thứ cùng tên "JWT", khác chỗ

  - **Phát** token (tạo, ký) → `Infrastructure/ExternalServices/`
  - **Xác thực** token (middleware kiểm mỗi request) → `API/Extensions/`

  ## Chưa có chỗ — hỏi trước khi làm

  - Tác vụ chạy ngầm (chuyển `PracticeSubmission` sang `Overdue`, nhắc xác nhận tham gia).
    Khi cần sẽ thêm thư mục riêng; chưa có thì dừng lại hỏi, đừng nhét vào `Services/`.
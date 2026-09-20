---
name: new-feature
description: Dùng khi thêm hoặc sửa chức năng ở backend Harmonia — dựng entity, repository, DTO, validator, service, controller theo đúng thứ tự layer. Kích hoạt khi người dùng nói "thêm chức năng", "thêm endpoint", "thêm API", "làm tính năng", "tạo API cho", "dựng module", hoặc mô tả một use case cần làm.
---

# Thêm chức năng mới cho Harmonia

Theo workflow trong `.claude/rules/04-workflow.md`. Dưới đây là phần riêng của việc
dựng tính năng mới: cần đọc gì ở READ, nội dung PLAN, và 13 bước IMPLEMENT.

## READ — đọc thêm

Ngoài phần READ chung, còn phải đối chiếu use case trong
`claude/use-case-descriptions.md` — đặc biệt xem có «include» UC-05 Send Notification không.

## PLAN — xác định thêm

- Use case nào, actor nào gọi — quyết định `[Authorize(Roles = ...)]`.
- Tên nghiệp vụ tiếng Anh lấy từ `.claude/rules/02-naming.md`.
  Không có trong bảng thì hỏi, đừng tự dịch.
- Chức năng **ghi dữ liệu** hay **chỉ đọc**.

### Chức năng chỉ đọc thì rút ngắn

Use case dạng "xem danh sách", "xem chi tiết", "xem báo cáo" bỏ bước 1, 2, 5, 11
(không entity mới, không `Request`, không validator, không EF configuration, không migration).
Chỉ làm: thêm method vào repository cũ → DTO → profile → service → controller.
Đừng tạo file rỗng cho đủ bộ.

### Thứ đã tồn tại thì SỬA, không tạo thêm

Chỉ **DTO** được phép sinh thêm. Mọi thứ khác là mở file cũ ra sửa:

| Thành phần | Đã tồn tại thì |
|---|---|
| Entity | Đủ property → bỏ qua. Thiếu → thêm vào entity cũ, cảnh báo ảnh hưởng tính năng cũ, cần migration |
| `I<Entity>Repository` | Thêm method vào interface cũ |
| `<Entity>Dto` | **Không sửa** — sẽ vỡ endpoint cũ. Tạo `<Entity>SummaryDto` / `<Entity>DetailDto` riêng |
| `<Entity>Profile` | Thêm `CreateMap` vào profile cũ. Profile thứ hai cùng cặp nguồn–đích → AutoMapper lỗi duplicate map |
| `<Entity>Service` | Cùng nghiệp vụ → thêm method. Tách service mới chỉ khi nghiệp vụ khác hẳn |
| `<Entity>Controller` | Thêm action vào controller cũ. Hai controller cùng resource → lỗi ambiguous route |
| `<Entity>Configuration` | Sửa file cũ. Hai configuration cùng entity → cái sau đè cái trước âm thầm |

## IMPLEMENT — 13 bước

Đúng thứ tự, không nhảy cóc, không làm ngược từ controller xuống.

1. **Entity** — `Domain/Entities/`. Kế thừa `BaseAuditableEntity`.
   Không data annotation, không `using Microsoft.*`.
   Có quan hệ với entity khác (thường là `LiturgicalEvent`) thì khai navigation property
   và khóa ngoại ở **cả hai phía**, sửa luôn configuration của entity kia.
2. **Enum / value object** — `Domain/Enums/`, `Domain/ValueObjects/`.
   Enum số ít, không hậu tố `Enum`.
3. **Interface repository** — `Application/Interfaces/IRepositories/`.
4. **DTO** — `Application/DTOs/`. `<Entity>Dto` trả ra, `<Hành động><Entity>Request` nhận vào.
5. **Validator** — `Application/Validators/`, FluentValidation.
6. **AutoMapper profile** — `Application/Mappings/`.
7. **Interface service + service** — trả `Result<T>`, danh sách trả `PagedList<T>`.
   Lấy người gọi từ `ICurrentUserService`, kiểm dữ liệu có thuộc `Choir` của họ không
   trước khi đọc/sửa. Sai `Choir` trả `404`, không trả `403`.
8. **Gửi notification nếu use case yêu cầu** — trong service, gọi `INotificationService`,
   KHÔNG gọi `IHubContext` trực tiếp.
9. **Đăng ký DI** — `Application/DependencyInjection.cs`.

> **Điểm dừng giữa chừng.** Liệt kê file đã làm ở 9 bước trên, đối chiếu với PLAN,
> nêu chỗ lệch nếu có. Chờ duyệt trước khi sang Infrastructure.

10. **Repository** — `Infrastructure/Repositories/`, đăng ký trong `Infrastructure/DependencyInjection.cs`.
11. **EF configuration** — `Infrastructure/Data/Configurations/`, thêm `DbSet` số nhiều
    vào `HarmoniaDbContext`. **Luôn cần migration** — báo, không tự chạy `dotnet ef`.
12. **Controller** — `API/Controllers/`. Route kebab-case số nhiều.
    Bắt buộc `[Authorize(Roles = ...)]`. Nhận `Request`, trả `Dto`.
13. **Unit test cho service** — `tests/Harmonia.Application.UnitTests/`.
    Tối thiểu: một case thành công, một case sai quyền `Choir`, một case không tìm thấy.

## Dừng lại và hỏi giữa chừng khi

- Cần thêm package chưa có trong `.csproj`.
- Cần thêm `ProjectReference` mới giữa hai project.
- Thuật ngữ nghiệp vụ không có trong từ điển.
- Database provider hoặc kiểu khóa chính chưa chốt mà bước đang làm cần biết.
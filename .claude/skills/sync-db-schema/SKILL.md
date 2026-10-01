---
name: sync-db-schema
description: Dùng khi cần đồng bộ entity, EF configuration và DbContext của Harmonia theo tài liệu thiết kế dữ liệu. Kích hoạt khi người dùng nói "sửa db theo tài liệu", "cập nhật entity theo doc", "đồng bộ schema", "đồng bộ ERD", "thêm bảng", "thêm cột", "đổi schema", hoặc đưa đường dẫn tới file danh sách entity.
---

# Đồng bộ schema theo tài liệu thiết kế

Theo workflow trong `.claude/rules/04-workflow.md`. Dưới đây là phần riêng của việc
đồng bộ schema.

**Một chiều: tài liệu → code.** Tài liệu thiết kế là nguồn chuẩn. Không bao giờ sửa
tài liệu cho khớp code.

## READ

Đọc tài liệu nguồn — mặc định `doc/domain-entity-list.md`; người dùng đưa đường dẫn
khác thì dùng đường dẫn đó. Không tìm thấy file thì **hỏi, đừng đoán**.

Rồi đọc trạng thái code hiện tại:

- `src/Harmonia.Domain/Entities/` và `Enums/`
- `src/Harmonia.Infrastructure/Data/HarmoniaDbContext.cs` — danh sách `DbSet`
- `src/Harmonia.Infrastructure/Data/Configurations/`
- `src/Harmonia.Infrastructure/Migrations/` — chỉ để biết migration gần nhất tên gì.
  **Không đọc kỹ, không sửa.**

## SUMMARIZE

Xuất bảng so sánh, mỗi dòng một khác biệt:

| Entity | Thuộc tính | Tài liệu | Code hiện tại | Loại thay đổi |
|---|---|---|---|---|

Loại thay đổi phải nói rõ: thêm bảng, xoá bảng, thêm cột, xoá cột, đổi kiểu,
đổi nullable, đổi unique/index, đổi enum, đổi quan hệ.

Không có khác biệt thì báo "đã khớp" và dừng, đừng tạo migration rỗng.

## PLAN

Phân loại **mức rủi ro dữ liệu** cho từng thay đổi — đây là phần quan trọng nhất:

| Mức | Thay đổi | Hệ quả |
|---|---|---|
| An toàn | Thêm bảng, thêm cột nullable, thêm index | Không mất gì |
| Cần giá trị mặc định | Thêm cột NOT NULL vào bảng đã có dữ liệu | Migration lỗi nếu không có default |
| **Mất dữ liệu** | Xoá cột, xoá bảng, thu hẹp kiểu (string(500) → string(100)) | Dữ liệu cũ biến mất |
| **Bẫy đổi tên** | Đổi tên cột hoặc bảng | EF sinh drop + add, dữ liệu cột cũ mất sạch |

Thay đổi ở hai mức dưới thì nêu rõ bằng chữ in đậm, và chờ người dùng xác nhận
từng cái một trước khi làm.

Nếu **code có thứ tài liệu không có**: dừng lại hỏi. Đó có thể là thứ ai đó mới thêm
mà chưa kịp ghi tài liệu — xoá đi là xoá công việc của người khác.

## IMPLEMENT

Đúng thứ tự:

1. **Enum** — `Domain/Enums/`. Số ít, không hậu tố `Enum`.
2. **Entity** — `Domain/Entities/`. Entity nào tài liệu đánh ***auditable*** thì kế thừa
   `BaseAuditableEntity`, còn lại kế thừa `BaseEntity`. Không data annotation,
   không `using Microsoft.*`.
3. **Quan hệ** — khai navigation property và khóa ngoại ở **cả hai phía**, theo đúng
   phần "Relationships" của tài liệu.
4. **EF configuration** — `Infrastructure/Data/Configurations/<Entity>Configuration.cs`.
   Khai đầy đủ: độ dài chuỗi, nullable, unique, khóa ngoại, hành vi xoá.
   Mọi ràng buộc unique trong tài liệu phải thành `HasIndex(...).IsUnique()`.
5. **DbSet** — thêm vào `HarmoniaDbContext`, tên số nhiều.
6. **Seed** — bảng lookup có dữ liệu cố định thì dùng `HasData()` trong configuration.

Tên entity, property, enum lấy **nguyên văn** từ tài liệu. Thấy tên không khớp
`.claude/rules/02-naming.md` thì báo, đừng tự sửa bên nào.

## VERIFY

- `dotnet build Harmonia.slnx`
- Đối chiếu lại từng dòng trong bảng SUMMARIZE: đã làm hết chưa, có làm thừa gì không.

## STOP

**KHÔNG chạy `dotnet ef`.** Đưa người dùng đúng hai lệnh và ba lời nhắc:

```bash
dotnet ef migrations add <TênMôTảThayĐổi> -p src/Harmonia.Infrastructure -s src/Harmonia.API
dotnet ef database update -p src/Harmonia.Infrastructure -s src/Harmonia.API
```

Nhắc:

1. Mở file migration vừa sinh ra đọc trước khi `database update`.
2. Nếu PLAN có thay đổi mức "mất dữ liệu" hoặc "bẫy đổi tên", kiểm trong file migration
   xem có `DropColumn` / `DropTable` nào ngoài dự kiến không. Đổi tên thì sửa tay thành
   `RenameColumn` / `RenameTable` để giữ dữ liệu.
3. Đặt tên migration theo nội dung nghiệp vụ — `AddRefreshTokenDeviceId`, không phải
   `Update1`.

## Chưa chốt — hỏi khi gặp

- Enum lưu xuống database dạng số hay chuỗi. Chưa thống nhất thì hỏi một lần rồi ghi
  quyết định vào `.claude/rules/02-naming.md`, đừng mỗi bảng một kiểu.
- Hành vi xoá (`DeleteBehavior`) cho khóa ngoại mà tài liệu không nói.
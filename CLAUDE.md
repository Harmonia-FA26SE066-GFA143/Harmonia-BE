# Harmonia — Hệ thống quản lý điều phối ca đoàn và Thánh nhạc phụng vụ

Đồ án capstone FA26SE066 (09/2026–03/2027). Backend .NET 9, Clean Architecture.
Giúp ca đoàn giáo xứ: quản lý thành viên + kỹ năng, lên lịch tập, quản lý thư viện
thánh ca, đề xuất/duyệt danh sách bài hát, phân công phục vụ, điểm danh.

Trả lời bằng tiếng Việt. Code, comment, tên biến bằng tiếng Anh.

## Rule bắt buộc

@.claude/rules/01-layer-boundaries.md
@.claude/rules/02-naming.md
@.claude/rules/03-security.md
@.claude/rules/04-workflow.md

## 4 actor

| Actor | Nền tảng | Vai trò |
|---|---|---|
| ParishPriest | Web | Duyệt/từ chối danh sách bài hát, khai báo chương trình phụng vụ |
| ChoirDirector | Web | Quản lý thành viên, duyệt kỹ năng, đề xuất bài hát, phân công phục vụ |
| ChoirMember / Instrumentalist | Mobile | Khai báo kỹ năng, xác nhận tham gia, nộp bản thu tập hát |
| Admin | Web | Quản lý tài khoản, cấu hình danh mục, xem báo cáo |

## Quy ước

- Truy cập dữ liệu qua Repository Pattern. Repository trả entity, service trả DTO.
- Entity ↔ DTO bằng AutoMapper, profile đặt ở `Application/Mappings/`.
- Validate request bằng FluentValidation ở `Application/Validators/`.
- Lỗi nghiệp vụ thường trả qua `Result<T>`, không ném exception.
  `NotFoundException` / `ValidationException` dành cho trường hợp thật sự bất thường,
  do `GlobalExceptionHandlingMiddleware` bắt.

## Trạng thái hiện tại

Số liệu thật về repo được hook `SessionStart` in ra ở đầu phiên — đọc chỗ đó,
đừng giả định class nào đã tồn tại.

TODO chưa chốt: database provider, kiểu khóa chính entity.
Gặp việc cần hai thứ này thì HỎI, đừng tự chọn.

## Luật làm việc

- Không cài package mới nếu chưa hỏi.
- Không chạy `dotnet ef migrations` hay `database update` nếu chưa được yêu cầu rõ.
- Không sửa `.env`.
- Không refactor ngoài phạm vi được giao.

## Lệnh

```bash
dotnet build Harmonia.Solution.slnx
dotnet run --project src/Harmonia.API                         # http://localhost:5259
dotnet run --project src/Harmonia.API --launch-profile https  # https://localhost:7112
dotnet test
```
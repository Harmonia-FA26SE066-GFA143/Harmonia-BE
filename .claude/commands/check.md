---
description: Soát vi phạm kiến trúc và quy ước trước khi commit
allowed-tools: Bash(git diff:*), Bash(git status:*), Bash(dotnet build:*), Read, Grep, Glob
---

Soát các file đã thay đổi so với HEAD. Chưa có commit nào hoặc không có thay đổi
thì soát toàn bộ `src/`.

Kiểm đúng 13 mục dưới đây, không phát sinh thêm. Mỗi phát hiện ghi một dòng:
`<file>:<dòng> — <vi phạm> — <cách sửa>`. Không có vi phạm thì ghi "sạch".

## Ranh giới layer
1. `Harmonia.Domain` có `using` nào ngoài `System.*` không — đặc biệt `Microsoft.*`,
   `System.ComponentModel.DataAnnotations`.
2. `Harmonia.Application` có `using Harmonia.Infrastructure` không.
3. File trong `API/Controllers/` có nhắc `DbContext`, `Repository`, hoặc inject
   trực tiếp repository không.
4. `.csproj` có `ProjectReference` nào ngược chiều mũi tên trong
   `.claude/rules/01-layer-boundaries.md` không.
5. `IHubContext` có xuất hiện ngoài `API/Services/` không — nghiệp vụ phải gọi
   `INotificationService`, không gọi SignalR trực tiếp.
   Và `Program.cs` có đăng ký DI nào lẽ ra thuộc `DependencyInjection.cs` không
   (ngoại lệ duy nhất được phép: `SignalRNotificationService`).

## Quy ước
6. Tên class/property có dùng từ sai từ điển không — `ChoirLeader`, `Season`,
   `Assignment`, `Event` (đối chiếu `.claude/rules/02-naming.md`).
7. Method async có thiếu hậu tố `Async` hoặc thiếu `CancellationToken` không.
8. Tên file DTO có đúng ba nhóm `<Entity>Dto` / `<Hành động><Entity>Request` /
   `<X>Response` không. Route controller có đúng kebab-case số nhiều không
   (`api/song-lists`, không phải `api/SongList`).

## Bảo mật
9. Endpoint nào thiếu `[Authorize]`. Và `[AllowAnonymous]` có xuất hiện ở đâu
   ngoài `login` và `forgot-password` không — đây là lỗi nặng hơn thiếu `[Authorize]`.
10. Controller có **nhận** entity Domain làm tham số action, hoặc **trả** thẳng
    entity thay vì DTO không.
11. `appsettings*.json` có chuỗi nào trông như connection string thật, password,
    hay JWT key không. Code có hardcode secret, hay log số điện thoại / email
    ở mức Information không.
12. Có `FromSqlRaw` nối chuỗi, `AllowAnyOrigin()`, `Clients.All`, hay Hub nào
    thiếu `[Authorize]` không.

## Ngôn ngữ
13. Chạy đúng lệnh này, không tự nghĩ pattern khác:

```
    rg -n '[^\x00-\x7F]' --glob '*.cs' src
    rg -n '[^\x00-\x7F]' src/Harmonia.API/.env.example src/Harmonia.API/appsettings*.json
```

    Báo nguyên văn mọi dòng lệnh này trả về. Mọi chuỗi và comment trong `.cs` và
    trong file cấu hình phải là ASCII tiếng Anh; thông báo cho người dùng đi qua mã
    lỗi trong `Application/Common/ErrorCodes.cs`.
    Lệnh trả về rỗng thì ghi "sạch" — đừng tự kết luận khi chưa chạy.
    (Tài liệu trong `doc/` và `.claude/` vẫn viết tiếng Việt, không nằm trong phạm vi.)

Cuối cùng chạy `dotnet build Harmonia.Solution.slnx` và báo kết quả.

KHÔNG tự sửa gì. Chỉ báo cáo. Tôi quyết định sửa cái nào.
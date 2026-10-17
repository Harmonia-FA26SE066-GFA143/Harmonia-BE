---
description: Soát vi phạm kiến trúc và quy ước trước khi commit
allowed-tools: Bash(git diff:*), Bash(git status:*), Bash(dotnet build:*), Read, Grep, Glob
---

Soát các file đã thay đổi so với HEAD. Chưa có commit nào hoặc không có thay đổi
thì soát toàn bộ `src/`.

Kiểm đúng 20 mục dưới đây, không phát sinh thêm. Mỗi phát hiện ghi một dòng:
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
6. File trong `Application/Services/` có `using Microsoft.EntityFrameworkCore`,
   `using Microsoft.AspNetCore.SignalR`, hay nhắc `HttpClient` không — service nghiệp vụ đi qua
   repository và interface trong `Interfaces/IServices/`.

## Quy ước
7. Tên class/property có dùng từ sai từ điển không — `ChoirLeader`, `Season`,
   `Assignment`, `Event` (đối chiếu `.claude/rules/02-naming.md`).
   Exception và mã lỗi cũng phải theo từ điển, không còn ngoại lệ (chốt 2026-10-13, thay quyết
   định 2026-09-29): `LiturgicalEventCancelledException` / `LITURGICAL_EVENT_CANCELLED`,
   `RosterAssignment...`, `RehearsalAttendance...`, `LiturgicalSeason...`.
8. Method async có thiếu hậu tố `Async` hoặc thiếu `CancellationToken` không.
   Bỏ qua method có chữ ký do framework quy định (middleware `InvokeAsync`, filter, `Hub`,
   `BackgroundService`); với các method đó chỉ báo nếu không truyền `RequestAborted` /
   `stoppingToken` xuống lời gọi bên trong.
9. Tên file DTO có đúng ba nhóm `<Entity>Dto` / `<Hành động><Entity>Request` /
   `<X>Response` không. Route controller có đúng kebab-case số nhiều không
   (`api/song-lists`, không phải `api/SongList`). Không tính `api/auth` và `api/schedule`
   — nhóm endpoint không phải tài nguyên, giữ số ít (xem `02-naming.md`).

## Validator và AutoMapper
10. Mỗi `<X>Request` trong `Application/DTOs/` có `<X>RequestValidator` trong
    `Application/Validators/` không — báo từng Request thiếu. Không tính `SendNotificationRequest`
    (dựng trong tiến trình, không nhận từ client). Và validator có `.WithErrorCode("...")` gõ chuỗi
    trực tiếp thay vì hằng `ErrorCodes.X` không, có nằm ngoài `Application/Validators/` không.
11. `CreateMap` có xuất hiện ngoài `Application/Mappings/` không. Mỗi file trong `Mappings/`
    có tên `<Entity>Profile` và chỉ xoay quanh **một** entity không (một entity ra nhiều DTO
    thì được, Request → chính entity đó cũng được). Và mỗi `<Entity>Dto` / `<Entity>DetailDto` / `<Entity>SummaryDto` mà service gọi
    `mapper.Map<...>` có `CreateMap` tương ứng không.

## Bảo mật
12. Endpoint nào thiếu `[Authorize]`. Và `[AllowAnonymous]` có xuất hiện ở đâu
    ngoài `login`, `google`, `refresh`, `forgot-password` và `reset-password` không — đây là lỗi nặng hơn thiếu `[Authorize]`.
13. Controller có **nhận** entity Domain làm tham số action, hoặc **trả** thẳng
    entity thay vì DTO không.
14. `appsettings*.json` có chuỗi nào trông như connection string thật, password,
    hay JWT key không. Code có hardcode secret, hay log số điện thoại / email
    ở mức Information không.
15. Có `FromSqlRaw` nối chuỗi, `AllowAnyOrigin()`, `Clients.All`, hay Hub nào
    thiếu `[Authorize]` không.

## Ngôn ngữ
16. Chạy đúng lệnh này, không tự nghĩ pattern khác:

```
    rg -n '[^\x00-\x7F]' --glob '*.cs' --glob '!**/ReportExportColumns.cs' src | rg -v -P 'Application.Services.\w+Service\.cs:\d+:(?!.*(//|Log|Exception)).*"'
    rg -n '[^\x00-\x7F]' src/Harmonia.API/.env.example src/Harmonia.API/appsettings*.json
```

    Ngoại lệ 1 (chốt 2026-10-11): chuỗi `Title` / `Content` của `SendNotificationRequest`
    trong `Application/Services/*Service.cs` — thông báo hiển thị nguyên văn cho người dùng.
    Vế `rg -v -P` của lệnh đầu đã lọc sẵn loại này: nó bỏ dòng có chuỗi trong
    `Application/Services/*Service.cs`, trừ dòng chứa `//`, `Log` hoặc `Exception` — nên
    comment, log, exception tiếng Việt trong cùng file vẫn bị báo. Bộ lọc không phân biệt
    được chuỗi thông báo với chuỗi khác trong service; nghi ngờ thì chạy lại không có vế lọc.
    Ngoại lệ 2 (chốt 2026-10-11): `Application/Services/ReportExportColumns.cs` — tên báo cáo và
    tiêu đề cột tiếng Việt của file xuất, đi thẳng tới Admin. Loại bằng `--glob` như ngoại lệ 1.
    Không mở rộng ngoại lệ sang chỗ khác.

    Báo nguyên văn mọi dòng lệnh này trả về. Mọi chuỗi và comment còn lại trong `.cs` và
    trong file cấu hình phải là ASCII tiếng Anh; thông báo lỗi cho người dùng đi qua mã
    lỗi trong `Domain/Common/ErrorCodes.cs`.
    Lệnh trả về rỗng thì ghi "sạch" — đừng tự kết luận khi chưa chạy.
    (Tài liệu trong `doc/` và `.claude/` vẫn viết tiếng Việt, không nằm trong phạm vi.)
17. `Title` / `Content` của mỗi `new SendNotificationRequest(` trong `Application/Services/` có
    viết tiếng Việt không — chuỗi chỉ toàn ASCII là vi phạm (ngược chiều với mục 16: thông báo
    hiển thị nguyên văn cho người dùng nên bắt buộc tiếng Việt).

## Tài liệu và test
18. Mỗi route trong `API/Controllers/` (`[Route]` của controller ghép với `[Http*]` của action)
    có dòng tương ứng trong `doc/api.md` không — báo từng endpoint thiếu.
19. Mỗi hằng trong `Domain/Common/ErrorCodes.cs` có trong `doc/error-codes.md` không — thiếu là
    vi phạm. Mã không có trong `API/Middlewares/ErrorStatusMap.cs` thì chỉ liệt kê kèm chữ
    "dùng status mặc định", không tính vi phạm (`StatusFor` có fallback).
20. Mỗi `<X>Service` trong `Application/Services/` có `<X>ServiceTests.cs` trong
    `test/Harmonia.Application.Tests/` không — báo từng service thiếu.

Cuối cùng chạy `dotnet build Harmonia.Solution.slnx` và báo kết quả.

KHÔNG tự sửa gì. Chỉ báo cáo. Tôi quyết định sửa cái nào.

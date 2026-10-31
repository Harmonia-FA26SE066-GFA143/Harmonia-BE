---
name: add-endpoint
description: Dùng khi thêm MỘT endpoint mới vào resource đã có của Harmonia, không cần entity hay bảng mới — thêm action vào controller cũ, method vào service/repository cũ. Kích hoạt khi người dùng nói "thêm 1 endpoint", "thêm api cho <resource đã có>", "thêm action", "cần api lấy/lọc/đếm ...", "thêm route con". Cần entity hoặc bảng mới thì dùng `new-feature`; sửa endpoint đang có thì dùng `change-endpoint`.
---

# Thêm một endpoint vào resource đã có

Theo workflow trong `.claude/rules/04-workflow.md` — đủ sáu giai đoạn, dừng chờ duyệt sau PLAN.

**Phạm vi:** entity đã có, controller đã có. Chỉ thêm, không đổi hành vi endpoint cũ.
- Cần entity / cột / bảng mới → dừng, chuyển `new-feature` (hoặc `sync-db-schema` cho cột).
- Phải sửa request/response của endpoint cũ → dừng, chuyển `change-endpoint`.

## READ

- Use case trong `doc/harmonia-use-case-descriptions.md` — actor nào, có «include» S-05
  Send Notification không.
- Controller của resource trong `src/Harmonia.API/Controllers/` — route gốc, `[Authorize]`
  ở cấp controller, các action đã có (tránh trùng route).
- Service đang phục vụ controller đó + interface trong `Application/Interfaces/IServices/`.
- Repository: `I<Entity>Repository` nếu có; không có thì service đang dùng `IGenericRepository<T>`.
- DTO, profile, validator sẵn có của entity — xem có tái dùng được không.
- `Domain/Common/ErrorCodes.cs` và `src/Harmonia.API/Middlewares/ErrorStatusMap.cs` nếu cần mã lỗi.
- Test hiện có: `test/Harmonia.Application.Tests/<X>ServiceTests.cs`,
  `test/Harmonia.API.Tests/<X>EndpointsTests.cs`.

## SUMMARIZE

In hợp đồng của endpoint mới, dạng gửi thẳng được cho FE/mobile:

```
Mới:  GET  api/rehearsals/{id}/absentees    [ChoirDirector]
      query: —   body: —
      → 200 RehearsalAttendanceDto[]
      → 404 REHEARSAL_NOT_FOUND
```

Kèm một dòng: endpoint này chỉ đọc hay có ghi dữ liệu.

## PLAN

- **Câu hỏi** — actor, phân trang hay không, ca viên có được gọi không (nếu có: chỉ bản ghi
  của chính mình, `SongList` `Approved`, `LiturgicalEvent` `Published`).
- **Route** — kebab-case, nằm dưới route gốc của controller. Route con không phải tài nguyên
  thì số ít (`default-songs`, `me`). Route tĩnh không được đè `{id:guid}`.
- **Quyền** — mọi role đăng nhập: không thêm gì (controller đã `[Authorize]`).
  Một phần role: `[Authorize(Roles = RoleNames.X)]` ở action. Không liệt kê đủ 4 role.
- **Danh sách file** — mỗi dòng một câu. Thường chỉ là SỬA file cũ:

| Thành phần | Làm gì |
|---|---|
| `I<Entity>Repository` + `<Entity>Repository` | Thêm method nếu cần truy vấn mới. Không viết LINQ trong service |
| `Request` (`Application/DTOs/`) | Chỉ khi có body/query nhiều trường — `<Hành động><Entity>Request` |
| `Dto` | Dùng lại nếu khớp. Không khớp → tạo `<Entity>SummaryDto`/`DetailDto`, **không sửa `<Entity>Dto` cũ** |
| Validator | Chỉ khi có `Request` mới — `<Request>Validator`, `.WithErrorCode(ErrorCodes.X)`. Tự đăng ký qua `AddValidatorsFromAssembly` |
| Profile | Thêm `CreateMap` vào `<Entity>Profile` cũ, không tạo profile thứ hai |
| `I<X>Service` + `<X>Service` | Thêm method vào service cũ, trả `Result<T>` / `PagedList<T>` |
| Controller | Thêm action vào controller cũ |
| `ErrorCodes.cs` + `ErrorStatusMap.cs` + `doc/error-codes.md` | Chỉ khi có mã lỗi mới — cả ba trong cùng thay đổi |
| Test | Unit test service + endpoint test |

Không đụng schema → không migration. Nếu phát hiện cần cột mới: dừng, báo.

## IMPLEMENT

Từ trong ra ngoài, controller cuối cùng. Bước không cần thì bỏ, không tạo file rỗng.

1. **Repository** — method đặt tên theo nghiệp vụ, `async` + `CancellationToken`, dùng
   property `DbContext` của `GenericRepository`. Phân trang: nhận `Search<X>Request` (kế thừa
   `PagingRequest`), trả `new PagedList<T>(items, filter.PageNumber, filter.PageSize, totalCount)`
   — xem mẫu `AuditLogRepository.SearchAsync`.
2. **DTO / Request** — mỗi file một class.
3. **Validator** — nếu có `Request`.
4. **Mapping** — thêm vào profile cũ.
5. **Service** — logic, kiểm quyền sở hữu qua `ICurrentUserService`
   (không thuộc người gọi → 404, không 403). Có ghi dữ liệu thì `SaveChangesAsync` một lần ở cuối.
   Use case có S-05 → gọi `INotificationService`, không gọi `IHubContext`.
6. **Controller** — một biểu thức, theo mẫu sẵn có:

   ```csharp
   [HttpGet("{id:guid}/absentees")]
   [Authorize(Roles = RoleNames.ChoirDirector)]
   public async Task<IActionResult> GetAbsenteesAsync(Guid id, CancellationToken cancellationToken) =>
       ToActionResult(await rehearsalAttendanceService.GetAbsenteesAsync(id, cancellationToken));
   ```

   Cần id người gọi thì `User.GetUserId()`. Không logic, không repository trong controller.
   Service mới chưa có trong constructor thì thêm vào primary constructor; service hoàn toàn
   mới thì đăng ký ở `Application/DependencyInjection.cs`.
7. **Test** — tối thiểu: một case thành công, một case không tìm thấy, một case sai quyền
   (role không được gọi → 403; bản ghi của người khác → 404). Token lấy từ
   `TestContext.Current.CancellationToken`, tên method hậu tố `_Async`.

## VERIFY

- `dotnet build Harmonia.Solution.slnx` và `dotnet test` — `ApiConventionTests` bắt lỗi
  thiếu `[Authorize]` và tên role lạ. Route kebab-case thì tự soát, không test nào kiểm.
- Đối chiếu hợp đồng thực tế với bảng SUMMARIZE.
- Soát `01-layer-boundaries.md` và `03-security.md` trên các file vừa đụng.

## STOP

Không commit. Báo lại:

1. Hợp đồng endpoint mới (khối SUMMARIZE).
2. File đã sửa / tạo.
3. Mã lỗi mới (nếu có) và đã cập nhật `doc/error-codes.md` chưa.
4. Nhắc chạy `/update-sheet` để đẩy endpoint lên `doc/api.md` và Google Sheet cho FE/mobile.

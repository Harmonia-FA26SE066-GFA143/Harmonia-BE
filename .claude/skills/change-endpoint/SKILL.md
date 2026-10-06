---
name: change-endpoint
description: Dùng khi sửa một endpoint API đã có của Harmonia — đổi request/response, đổi quyền, đổi route, thêm trường, siết hoặc nới validation. Kích hoạt khi người dùng nói "sửa api", "sửa endpoint", "đổi response", "thêm trường vào api", "đổi quyền endpoint", "bỏ trường", hoặc chỉ vào một endpoint đang có và yêu cầu thay đổi.
---

# Sửa endpoint đã có

Theo workflow trong `.claude/rules/04-workflow.md`.

**Việc khó nhất ở đây không phải sửa code, mà là biết ai đang dùng nó.** Web của cha xứ,
web của ca trưởng, web admin và app mobile đều gọi API này. Một thay đổi hai phút ở BE
có thể làm trắng màn hình bên mobile mà không ai biết cho tới lúc demo.

**Không đụng entity trong skill này.** Thay đổi cần sửa bảng hay cột → dừng, chuyển sang
`sync-db-schema`, xong rồi quay lại.

## READ

Tìm và đọc toàn bộ chuỗi của endpoint đang sửa:

- Controller và action — route, HTTP method, `[Authorize(Roles = ...)]`
- `Request` và `Dto` liên quan trong `Application/DTOs/`
- Validator trong `Application/Validators/`
- Service xử lý, và mã lỗi nó trả về
- `AutoMapper` profile liên quan
- Test trong `tests/Harmonia.Application.UnitTests/`
- `claude/error-codes.md` nếu thay đổi đụng tới mã lỗi

## SUMMARIZE

In ra **hợp đồng hiện tại** rồi **hợp đồng sau khi sửa**, cạnh nhau:

```
Trước:  GET  api/song-lists/{id}    [ChoirDirector, ParishPriest]
        → SongListDto { id, eventId, version, status, items[] }
        → 404 SONG_LIST_NOT_FOUND

Sau:    GET  api/song-lists/{id}    [ChoirDirector, ParishPriest, ChoirMember]
        → SongListDto { id, eventId, version, status, items[], approvedAt }
        → 404 SONG_LIST_NOT_FOUND
        → 409 SONG_LIST_NOT_APPROVED   ← mã mới
```

## PLAN

Phân loại thay đổi. Đây là phần quan trọng nhất của skill này:

| An toàn với client | Phá client |
|---|---|
| Thêm trường vào response | Xoá hoặc đổi tên trường trong response |
| Thêm trường **không bắt buộc** vào request | Thêm trường **bắt buộc** vào request |
| Thêm endpoint mới | Đổi route hoặc HTTP method |
| Nới lỏng validation | Siết chặt validation |
| Mở rộng danh sách role được gọi | Thu hẹp danh sách role |
| | Đổi kiểu dữ liệu một trường |
| | Đổi HTTP status trả về |
| | Thêm mã lỗi mới mà FE chưa có bản dịch |
| | Thêm giá trị enum mới (client cũ không hiểu) |

Thay đổi nằm cột phải thì ghi rõ **PHÁ CLIENT**, liệt kê bên nào phải sửa theo
(web cha xứ / web ca trưởng / web admin / mobile), và chờ người dùng xác nhận.

## IMPLEMENT

Từ trong ra ngoài, controller sửa cuối cùng. Bước nào không cần thì bỏ, đừng tạo file rỗng.

1. **Repository** — cần dữ liệu mới hay điều kiện lọc mới thì thêm method vào
   `I<Entity>Repository` và cài đặt bên `Infrastructure/Repositories/`.
   Đặt tên theo nghiệp vụ. **Không viết LINQ trong service.**
2. **DTO / Request** — `Application/DTOs/`.
   Trường mới chỉ phục vụ một màn hình thì cân nhắc `<Entity>DetailDto` riêng,
   đừng bơm thêm vào Dto mà ba màn hình khác đang dùng.
3. **Validator** — `Application/Validators/`. Quy tắc mới dùng `.WithErrorCode(ErrorCodes.X)`.
4. **Mapping** — `Application/Mappings/`, thêm `CreateMap` cho trường mới.
5. **Service** — logic và mã lỗi. Trả `Result<T>`, danh sách trả `PagedList<T>`.
   Mã lỗi mới khai hằng trong `Application/Common/ErrorCodes.cs` trước khi dùng.
   Thay đổi có ghi dữ liệu thì gọi `IUnitOfWork.SaveChangesAsync` đúng một lần ở cuối.
6. **Kiểm quyền** — nếu thay đổi đụng tới `[Authorize]`, tới ai được xem, hay tới
   trạng thái bản ghi được trả về, đọc lại `.claude/rules/03-security.md` và soát:
   - Ca viên chỉ thao tác trên bản ghi **của chính mình**.
   - Ca viên chỉ xem `SongList` ở trạng thái `Approved`, `LiturgicalWeek` ở `Published`.
   - Không thuộc phạm vi người gọi thì trả **404**, không trả 403.
   Kiểm ở service, không chỉ dựa vào attribute.
7. **Controller** — route, method, `[Authorize]`, kiểu trả về.
8. **Test** — sửa test cũ cho khớp, thêm test cho nhánh mới và cho ca sai quyền.

## VERIFY

- `dotnet build Harmonia.slnx` và `dotnet test`
- Chạy API, mở Swagger, **đọc lại hợp đồng mới trên đó** — đúng với bảng SUMMARIZE chưa.
- Thử bằng tài khoản của role **không** được phép gọi: phải ra 403, hoặc 404 nếu là
  bản ghi không thuộc người gọi.
- Test cũ nào đỏ thì đọc kỹ: nó đỏ vì code sai, hay vì hợp đồng cố ý đổi. Đừng sửa
  test cho xanh mà chưa biết lý do.
- Thay đổi có nới role hoặc mở rộng dữ liệu trả về: thử bằng tài khoản ca viên,
  xác nhận không nhìn thấy bản ghi chưa duyệt hay bản ghi của người khác.

## STOP

Không commit. Báo lại ba thứ:

1. Hợp đồng trước / sau, dạng như mục SUMMARIZE — đây là thứ gửi cho nhóm FE và mobile.
2. Có mã lỗi mới không. Có thì nhắc cập nhật `claude/error-codes.md` và `doc/error-codes.md`
   **trong cùng PR**, nếu không FE sẽ hiển thị mã thô cho ca viên xem.
3. Thay đổi phá client thì nói rõ bên nào phải sửa gì, và PR này **không được merge**
   trước khi hai bên hẹn nhau.

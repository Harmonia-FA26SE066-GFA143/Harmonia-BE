# Harmonia – Error Code Catalogue (BE ↔ FE contract)

BE trả `code`, FE tra bảng này để hiển thị tiếng Việt.

## Hình dạng response lỗi

```json
{
  "code": "SONG_LIST_ALREADY_APPROVED",
  "message": "Song list has already been approved",
  "errors": { "title": ["SONG_LIST_TITLE_REQUIRED"] }
}
```

`code` — FE dùng để tra bảng. `message` tiếng Anh, cho lập trình viên đọc log và hiển
thị tạm khi FE chưa có bản dịch cho mã mới. `errors` chỉ có ở lỗi validation (400).

Mã khai thành hằng trong `Domain/Common/ErrorCodes.cs`, không gõ chuỗi trực tiếp.
Đặt ở Domain vì cả `DomainException` (Domain) lẫn Service/Validator (Application) đều
dùng, mà Domain không được tham chiếu Application.
Validation dùng `.WithErrorCode(ErrorCodes.X)` của FluentValidation.

## Cách lỗi đi từ BE tới response (phương án A — đã chốt 2026-09-21)

Error code là hợp đồng với FE — mọi response lỗi đều có `code`. Bên trong BE, lỗi đi
tới response theo đúng một trong bốn đường:

| Lỗi phát hiện ở đâu | Cách truyền | Ai trả response |
|---|---|---|
| Request sai định dạng / thiếu trường | FluentValidation `.WithErrorCode(...)` | Pipeline validation → 400 + `errors` |
| Service kiểm tra, lỗi **lường trước được**: không tìm thấy, trùng, sai trạng thái giữa nhiều aggregate, cần query DB | `Result<T>.Failure(ErrorCodes.X)` | Controller map `code` → HTTP |
| Entity bị gọi vi phạm luật của chính nó | **ném** `DomainException` (có `Code`) | `GlobalExceptionHandlingMiddleware` bắt → HTTP theo bảng dưới |
| Sự cố không lường trước (bug, DB sập) | exception tự nhiên | Middleware → 500 `INTERNAL_ERROR`, không lộ message gốc ở Production |

Quy tắc ngắn: **Service chủ động kiểm tra → `Result<T>`; luật trong entity → throw.**
Service **không** `try/catch` `DomainException` — để nó bay lên middleware.
Dịch vụ ngoài: Infrastructure bắt exception của thư viện, trả `Result.Failure(EXTERNAL_*)`.

`DomainException` không biết HTTP. Middleware tra `code` → status theo cột HTTP của
file này (bảng tra đặt ở API), mặc định 409 nếu không có trong bảng.
Danh sách exception nào ném mã nào: xem `domain-exceptions.md`.

`NotFoundException` (Application) chỉ dùng khi dữ liệu **lẽ ra phải có** mà mất (lỗi
hệ thống). Người dùng gửi id không tồn tại → `Result.Failure(XXX_NOT_FOUND)`.

**Danh sách này là tham chiếu, không phải checklist.** Chỉ thêm hằng khi làm tới chức
năng tương ứng. Thêm mã mới thì phải cập nhật file này trong cùng PR.

---

## 0. Dùng chung

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Dữ liệu không hợp lệ |
| `UNAUTHORIZED` | 401 | Bạn cần đăng nhập |
| `FORBIDDEN` | 403 | Bạn không có quyền thực hiện thao tác này |
| `NOT_FOUND` | 404 | Không tìm thấy dữ liệu |
| `CONFLICT` | 409 | Thao tác xung đột với trạng thái hiện tại |
| `INTERNAL_ERROR` | 500 | Lỗi hệ thống, vui lòng thử lại |

Bốn mã lookup dùng chung cho 9 bảng danh mục (`Role`, `SkillCategory`,
`LiturgicalSeason`, `MassType`, `CeremonyType`, `EventCategory`, `WorshipLocation`,
`SongTheme`, `LiturgicalSlot`):

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `LOOKUP_NOT_FOUND` | 404 | Không tìm thấy mục danh mục |
| `LOOKUP_NAME_DUPLICATE` | 409 | Tên này đã tồn tại |
| `LOOKUP_IN_USE` | 409 | Mục này đang được sử dụng, không thể vô hiệu hoá |
| `LOOKUP_INACTIVE` | 409 | Mục này đã bị vô hiệu hoá |

## 1. Auth & tài khoản

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `AUTH_INVALID_CREDENTIALS` | 401 | Sai tài khoản hoặc mật khẩu |
| `AUTH_ACCOUNT_INACTIVE` | 403 | Tài khoản đã bị vô hiệu hoá |
| `AUTH_TOKEN_INVALID` | 401 | Phiên đăng nhập không hợp lệ |
| `AUTH_TOKEN_EXPIRED` | 401 | Phiên đăng nhập đã hết hạn |
| `AUTH_REFRESH_TOKEN_NOT_FOUND` | 401 | Phiên đăng nhập không tồn tại |
| `AUTH_REFRESH_TOKEN_REVOKED` | 401 | Phiên đăng nhập đã bị thu hồi |
| `AUTH_REFRESH_TOKEN_EXPIRED` | 401 | Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại |
| `AUTH_CURRENT_PASSWORD_INVALID` | 400 | Mật khẩu hiện tại không đúng |
| `AUTH_PASSWORD_TOO_WEAK` | 400 | Mật khẩu chưa đủ mạnh |
| `AUTH_RESET_TOKEN_INVALID` | 400 | Liên kết đặt lại mật khẩu không hợp lệ |
| `AUTH_RESET_TOKEN_EXPIRED` | 400 | Liên kết đặt lại mật khẩu đã hết hạn |
| `AUTH_RESET_TOKEN_USED` | 400 | Liên kết đặt lại mật khẩu đã được sử dụng |

**Không có mã cho "email không tồn tại".** `forgot-password` luôn trả 200 dù email có
thật hay không — theo `.claude/rules/03-security.md`, để không lộ email nào đã đăng ký.

## 2. User & Role (Admin)

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `USER_NOT_FOUND` | 404 | Không tìm thấy tài khoản |
| `USER_EMAIL_ALREADY_EXISTS` | 409 | Email này đã được sử dụng |
| `USER_ALREADY_INACTIVE` | 409 | Tài khoản đã ở trạng thái vô hiệu |
| `USER_CANNOT_MODIFY_SELF` | 409 | Không thể tự đổi quyền hoặc vô hiệu hoá chính mình |
| `USER_LAST_ADMIN` | 409 | Không thể xoá quyền của quản trị viên cuối cùng |
| `ROLE_NOT_FOUND` | 404 | Không tìm thấy vai trò |

## 3. MemberProfile

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `MEMBER_NOT_FOUND` | 404 | Không tìm thấy ca viên |
| `MEMBER_PROFILE_ALREADY_EXISTS` | 409 | Tài khoản này đã có hồ sơ ca viên |
| `MEMBER_NOT_ACTIVE` | 409 | Ca viên không còn hoạt động |
| `MEMBER_JOINED_DATE_IN_FUTURE` | 400 | Ngày gia nhập không được ở tương lai |

## 4. Skill & MemberSkill

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `SKILL_NOT_FOUND` | 404 | Không tìm thấy kỹ năng |
| `SKILL_NAME_DUPLICATE` | 409 | Kỹ năng này đã tồn tại trong nhóm |
| `SKILL_INACTIVE` | 409 | Kỹ năng đã bị vô hiệu hoá |
| `SKILL_CATEGORY_NOT_FOUND` | 404 | Không tìm thấy nhóm kỹ năng |
| `MEMBER_SKILL_NOT_FOUND` | 404 | Không tìm thấy kỹ năng đã khai báo |
| `MEMBER_SKILL_ALREADY_DECLARED` | 409 | Bạn đã khai báo kỹ năng này rồi |
| `MEMBER_SKILL_ALREADY_REVIEWED` | 409 | Kỹ năng này đã được duyệt hoặc từ chối |
| `MEMBER_SKILL_REJECT_REASON_REQUIRED` | 400 | Vui lòng nhập lý do từ chối |
| `MEMBER_SKILL_NOT_APPROVED` | 409 | Kỹ năng chưa được duyệt |

## 5. Lịch phụng vụ

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `WEEK_NOT_FOUND` | 404 | Không tìm thấy tuần phụng vụ |
| `WEEK_ALREADY_EXISTS` | 409 | Tuần này đã được tạo |
| `WEEK_START_NOT_MONDAY` | 400 | Tuần phải bắt đầu vào thứ Hai |
| `WEEK_ALREADY_PUBLISHED` | 409 | Tuần đã công bố, không thể sửa |
| `WEEK_NOT_PUBLISHED` | 409 | Tuần chưa được công bố |
| `EVENT_NOT_FOUND` | 404 | Không tìm thấy sự kiện |
| `EVENT_SLOT_TAKEN` | 409 | Đã có sự kiện khác vào giờ này tại địa điểm này |
| `EVENT_DATE_OUTSIDE_WEEK` | 400 | Ngày sự kiện không nằm trong tuần đã chọn |
| `EVENT_CANCELLED` | 409 | Sự kiện đã bị huỷ |
| `EVENT_ALREADY_PASSED` | 409 | Sự kiện đã diễn ra |
| `EVENT_TYPE_REQUIRED` | 400 | Phải chọn loại lễ hoặc loại nghi thức |
| `SEASON_DATE_OVERLAP` | 409 | Khoảng thời gian trùng với mùa phụng vụ khác |
| `SEASON_DATE_INVALID` | 400 | Ngày kết thúc phải sau ngày bắt đầu |

## 6. Thư viện thánh ca

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `SONG_NOT_FOUND` | 404 | Không tìm thấy bài hát |
| `SONG_INACTIVE` | 409 | Bài hát đã bị vô hiệu hoá |
| `SONG_TITLE_DUPLICATE` | 409 | Bài hát cùng tên đã tồn tại |
| `SONG_CLASSIFICATION_DUPLICATE` | 409 | Bài hát đã được phân loại theo mục này |
| `SONG_CLASSIFICATION_TARGET_INVALID` | 400 | Đối tượng phân loại không hợp lệ |
| `SONG_SKILL_REQUIREMENT_DUPLICATE` | 409 | Kỹ năng này đã được khai báo cho bài hát |
| `MATERIAL_NOT_FOUND` | 404 | Không tìm thấy tài liệu |
| `MATERIAL_FILE_REQUIRED` | 400 | Vui lòng chọn tệp |
| `MATERIAL_FILE_TYPE_NOT_ALLOWED` | 400 | Định dạng tệp không được hỗ trợ |
| `MATERIAL_FILE_TOO_LARGE` | 413 | Tệp vượt quá dung lượng cho phép |
| `MATERIAL_LEARNING_STATUS_INVALID` | 400 | Trạng thái học tập không hợp lệ |

## 7. Danh sách bài hát & duyệt

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `SONG_LIST_NOT_FOUND` | 404 | Không tìm thấy danh sách bài hát |
| `SONG_LIST_NOT_EDITABLE` | 409 | Danh sách đã gửi duyệt, không thể sửa |
| `SONG_LIST_NOT_LATEST_VERSION` | 409 | Chỉ được thao tác trên phiên bản mới nhất |
| `SONG_LIST_EMPTY` | 400 | Danh sách phải có ít nhất một bài hát |
| `SONG_LIST_ALREADY_SUBMITTED` | 409 | Danh sách đã được gửi duyệt |
| `SONG_LIST_NOT_SUBMITTED` | 409 | Danh sách chưa được gửi duyệt |
| `SONG_LIST_ALREADY_APPROVED` | 409 | Danh sách đã được phê duyệt |
| `SONG_LIST_EVENT_HAS_APPROVED_VERSION` | 409 | Sự kiện này đã có danh sách được duyệt |
| `SONG_LIST_ITEM_NOT_FOUND` | 404 | Không tìm thấy bài hát trong danh sách |
| `SONG_LIST_SLOT_DUPLICATE` | 409 | Vị trí phụng vụ này đã có bài hát |
| `SLOT_NOT_FOUND` | 404 | Không tìm thấy vị trí phụng vụ |
| `SONG_LIST_CANNOT_BE_REVISED` | 409 | Chỉ tạo phiên bản mới khi danh sách bị từ chối hoặc cần sửa |
| `REVIEW_NOTES_REQUIRED` | 400 | Vui lòng nhập ghi chú khi từ chối hoặc yêu cầu sửa |

Duyệt một phiên bản đã được quyết định → `SONG_LIST_NOT_SUBMITTED` (mỗi phiên bản chỉ có
một quyết định; đã quyết thì `Status` không còn `Submitted`).

## 8. Xác nhận tham gia

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `PARTICIPATION_NOT_FOUND` | 404 | Không tìm thấy yêu cầu tham gia |
| `PARTICIPATION_ALREADY_REQUESTED` | 409 | Đã gửi yêu cầu cho ca viên này |
| `PARTICIPATION_ALREADY_RESPONDED` | 409 | Bạn đã phản hồi yêu cầu này |
| `PARTICIPATION_EVENT_PASSED` | 409 | Sự kiện đã diễn ra, không thể phản hồi |
| `PARTICIPATION_NOT_INVITED` | 409 | Bạn chưa được mời tham gia sự kiện này |

## 9. Phân công phục vụ

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `ROSTER_NOT_FOUND` | 404 | Không tìm thấy bảng phân công |
| `ROSTER_ALREADY_EXISTS` | 409 | Sự kiện này đã có bảng phân công |
| `ROSTER_ALREADY_FINALIZED` | 409 | Bảng phân công đã chốt, không thể sửa |
| `ROSTER_SONG_LIST_NOT_APPROVED` | 409 | Phải duyệt danh sách bài hát trước khi chốt phân công |
| `ROSTER_NO_PERSONNEL_REQUIREMENT` | 409 | Chưa khai báo nhân sự cần cho các bài hát |
| `ROSTER_INSUFFICIENT_MEMBERS` | 409 | Không đủ người cho một số bè hoặc nhạc cụ |
| `ASSIGNMENT_NOT_FOUND` | 404 | Không tìm thấy dòng phân công |
| `ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED` | 409 | Ca viên chưa được duyệt kỹ năng này |
| `ASSIGNMENT_MEMBER_NOT_CONFIRMED` | 409 | Ca viên chưa xác nhận tham gia |
| `ASSIGNMENT_DUPLICATE` | 409 | Ca viên đã được phân công vị trí này |
| `PERSONNEL_REQUIREMENT_NOT_FOUND` | 404 | Không tìm thấy yêu cầu nhân sự |
| `PERSONNEL_REQUIRED_COUNT_INVALID` | 400 | Số lượng người phải lớn hơn 0 |
| `PERSONNEL_REQUIREMENT_DUPLICATE` | 409 | Kỹ năng này đã được khai báo cho bài hát |

## 10. Buổi tập & điểm danh

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `REHEARSAL_NOT_FOUND` | 404 | Không tìm thấy buổi tập |
| `REHEARSAL_TIME_INVALID` | 400 | Giờ kết thúc phải sau giờ bắt đầu |
| `REHEARSAL_TIME_CONFLICT` | 409 | Địa điểm đã có buổi tập khác vào giờ này |
| `REHEARSAL_ALREADY_PASSED` | 409 | Buổi tập đã diễn ra |
| `ATTENDANCE_NOT_FOUND` | 404 | Không tìm thấy bản ghi điểm danh |
| `ATTENDANCE_ALREADY_RECORDED` | 409 | Ca viên này đã được điểm danh |

## 11. Bài tập & bản thu

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `PRACTICE_ASSIGNMENT_NOT_FOUND` | 404 | Không tìm thấy bài tập |
| `PRACTICE_DUE_DATE_IN_PAST` | 400 | Hạn nộp không được ở quá khứ |
| `PRACTICE_TARGET_REQUIRED` | 400 | Vui lòng chọn ca viên hoặc nhóm kỹ năng nhận bài tập |
| `PRACTICE_NOT_ASSIGNED_TO_MEMBER` | 409 | Bài tập này không dành cho bạn |
| `PRACTICE_SUBMISSION_NOT_FOUND` | 404 | Không tìm thấy bản thu |
| `PRACTICE_SUBMISSION_PAST_DUE` | 409 | Đã quá hạn nộp |
| `PRACTICE_SUBMISSION_ALREADY_REVIEWED` | 409 | Bản thu này đã được nhận xét |
| `PRACTICE_AUDIO_REQUIRED` | 400 | Vui lòng chọn tệp ghi âm |
| `PRACTICE_AUDIO_TYPE_NOT_ALLOWED` | 400 | Định dạng ghi âm không được hỗ trợ |
| `PRACTICE_AUDIO_TOO_LARGE` | 413 | Tệp ghi âm vượt quá dung lượng cho phép |

## 12. Thông báo

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `NOTIFICATION_NOT_FOUND` | 404 | Không tìm thấy thông báo |
| `NOTIFICATION_NOT_FOR_USER` | 403 | Thông báo này không dành cho bạn |
| `DIRECTOR_NOTE_TARGET_REQUIRED` | 400 | Phải chọn tuần hoặc sự kiện để gửi ghi chú |

## 13. Hệ thống & báo cáo

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `SETTING_NOT_FOUND` | 404 | Không tìm thấy cấu hình |
| `SETTING_VALUE_TYPE_MISMATCH` | 400 | Giá trị không đúng kiểu dữ liệu đã khai báo |
| `REPORT_TYPE_NOT_SUPPORTED` | 400 | Loại báo cáo không được hỗ trợ |
| `REPORT_DATE_RANGE_INVALID` | 400 | Khoảng thời gian không hợp lệ |
| `REPORT_DATE_RANGE_TOO_LARGE` | 400 | Khoảng thời gian quá dài |
| `REPORT_NO_DATA` | 404 | Không có dữ liệu trong khoảng thời gian này |

## 14. Dịch vụ ngoài

| Mã | HTTP | Tiếng Việt |
|---|---|---|
| `EXTERNAL_EMAIL_FAILED` | 502 | Không gửi được email, vui lòng thử lại |
| `EXTERNAL_STORAGE_FAILED` | 502 | Không tải được tệp lên, vui lòng thử lại |
| `EXTERNAL_AI_FAILED` | 502 | Không tạo được gợi ý phân công, vui lòng thử lại |

---

## Quy ước chọn HTTP status

| Status | Khi nào |
|---|---|
| 400 | Dữ liệu đầu vào sai định dạng hoặc vi phạm ràng buộc đơn giản |
| 401 | Chưa đăng nhập, token sai hoặc hết hạn |
| 403 | Đã đăng nhập nhưng không đủ quyền |
| 404 | Không tìm thấy — **kể cả khi tài nguyên tồn tại nhưng không thuộc phạm vi của người gọi** |
| 409 | Trạng thái hiện tại không cho phép thao tác, hoặc vi phạm ràng buộc unique |
| 413 | Tệp vượt dung lượng |
| 502 | Dịch vụ ngoài lỗi (email, lưu trữ, AI) |
| 500 | Lỗi không lường trước |

Phân biệt 400 và 409: 400 là "dữ liệu bạn gửi sai", 409 là "dữ liệu đúng nhưng trạng
thái hệ thống không cho phép". Gửi tên bài hát rỗng là 400; gửi duyệt danh sách đã
duyệt rồi là 409.

Không bao giờ trả 403 cho trường hợp tài nguyên tồn tại nhưng người gọi không được
xem — trả 404, để không lộ sự tồn tại của bản ghi.

## Nguồn
- 2026-09-20: tạo từ `claude/domain-entity-list.md` (42 entity, 21 enum) và các ràng
  buộc unique / chuyển trạng thái trong đó.
- 2026-09-21: chốt phương án A (entity ném `DomainException`, middleware bắt). Thêm mục
  "Cách lỗi đi từ BE tới response"; dời `ErrorCodes.cs` sang `Domain/Common`; thêm
  `SONG_LIST_CANNOT_BE_REVISED`; bỏ `REVIEW_ALREADY_DECIDED` (trùng
  `SONG_LIST_NOT_SUBMITTED`). Đồng bộ với `domain-exceptions.md`.

---
description: Điền Report5 Unit Test (sheet Functions, một tab mỗi function, Statistics) từ test/Harmonia.Application.Tests, ghi thẳng lên link Google Sheet
argument-hint: <link Google Sheet của Report5> [F1,F8-04 — chỉ dựng lại các tab này]
allowed-tools: Read, Edit, Bash(dotnet test:*), Bash(python -P .claude/scripts/unit-test-report/build_functions_sheet.py:*), Bash(mkdir:*), mcp__claude_ai_Google_Drive__get_file_metadata
---

Điền mẫu báo cáo Unit Test (Report 5, FPTU) từ test hiện có, sửa trực tiếp trên Google Sheet.
Tham số: $ARGUMENTS — đầu tiên là link Sheet, sau đó (tuỳ chọn) danh sách mã function cần dựng lại tab.

## Điều kiện

- Python có `google-api-python-client` + `google-auth`. Chạy bằng `python -P` (không `-I`: `-I` bỏ qua package cài
  `--user`; `-P` vẫn chặn nạp `.py` lạ từ thư mục hiện tại).
- File key JSON của service account, **nằm ngoài repo**, đường dẫn đặt ở biến môi trường `HARMONIA_SHEETS_KEY`
  (hoặc truyền `--key`). Không đọc, không in, không copy nội dung key. Thiếu → dừng, chỉ người dùng cách tạo:
  Google Cloud project → bật Google Sheets API → Service account → Keys → JSON.
- Sheet phải được **Share cho email service account với quyền Editor**. Lỗi 403 / `PERMISSION_DENIED` → nhắc việc này.
- Không có link → hỏi, đừng đoán.

## Script làm gì

`.claude/scripts/unit-test-report/build_functions_sheet.py` (quét test, dựng nội dung) +
`sheets_writer.py` (ghi qua Sheets API), nguồn chỉ `test/Harmonia.Application.Tests`:

- **Functions**: một dòng một method được test (test gán vào method nó gọi trên `_sut`; validator →
  `<Validator>.Validate`). Requirement Name + mã (`F1-01`…) theo `requirement_map.json` — nhóm F<n> của
  `docs/harmonia-feature-list.md`. Project Name / Code ghi vào `Cover` (Functions đọc qua công thức).
- **Một tab mỗi function**, tên tab = tên function (trùng tên giữa nhiều class thì `Class.Method`, vd `SongService.GetByIdAsync`), nhân bản từ tab mẫu `Function 1`; mẫu đã bị xoá thì từ một tab
  function đã có. Tab cần dựng lại thì xoá rồi nhân bản lại tại đúng vị trí. Mỗi cột UTCID = một test case đã chạy
  trong trx (mỗi dòng `InlineData` / `MemberData` một cột):
  - Condition: `Precondition` + mỗi **input** của lời gọi service một nhóm (tham số của method; request `new X { A = .. }`
    tách từng property; biến cục bộ, hàm phụ của lớp test được lần ra biểu thức gốc; giá trị theory lấy từ trx), đánh `O`.
    Không có nhóm Scenario (thầy yêu cầu input).
  - Confirm: `Return` = phần kỳ vọng trong tên test + tham số `expected*` / `*Code`; `Exception` = kiểu trong
    `Assert.Throws*<X>`; `Log message` để trống.
  - Result: Type N/A/B đoán theo từ khoá (lỗi / exception → A, biên → B, còn lại N); P/F và ngày chạy lấy từ trx.
  - Header: Created / Executed By = `git config user.name`, Lines of code = số dòng của method trong service.
  - Cắt các dòng trống dưới phần Result để file không chạm giới hạn 10 triệu ô của Google.
- **Cover**: Creator (`git config user.name`), Issue Date (ngày chạy trong trx), Document Code
  `<code>_Report5_Unit Test_v1.0`, Version, một dòng Record of change (số function / case / pass / fail).
  **Statistics** (đầu trang): Creator, Document Code, Issue Date, Notes = danh sách 15 module F. Reviewer để trống.
- Xoá 3 tab mẫu `Function 1/2/3`, dựng lại `Statistics` (một dòng công thức mỗi tab + Sub total; biểu đồ tự theo),
  cột Sheet Name (Functions) / Function code (Statistics) là `=HYPERLINK("#gid=…")` tới tab.

## Các bước

### 1. Kiểm tra link
Tách id từ link (`/d/<id>/`). Gọi `get_file_metadata` (`excludeContentSnippets: true`):
- `mimeType` khác `application/vnd.google-apps.spreadsheet` (là `.xls` / `.xlsx`, link thường có `rtpof=true`) →
  **dừng**, bảo người dùng mở bằng tài khoản chủ file → *File → Save as Google Sheets* → gửi link file mới (id khác).
- Connector không xem được file thì bỏ qua bước này, cứ chạy script (service account có quyền là đủ).

### 2. Chạy test lấy kết quả
```bash
mkdir -p "<scratchpad>/unit-test-report/<giờ-phút>"
dotnet test --project test/Harmonia.Application.Tests --report-xunit-trx --report-xunit-trx-filename app.trx --results-directory "<thư mục trên>/trx"
```
Runner là Microsoft.Testing.Platform (`global.json`): `--logger trx` / `--report-trx` không chạy. Có test fail thì
vẫn đi tiếp (báo cáo ghi `F`), nhưng nói rõ cho người dùng. Build lỗi → dừng, báo lỗi.

### 3. Chạy script
```bash
python -P .claude/scripts/unit-test-report/build_functions_sheet.py --repo . --trx "<...>/trx/app.trx" --sheet "<link>" [--only F1,F8-04]
```
- Có tham số thứ hai trong `$ARGUMENTS` thì truyền vào `--only`. Không có → dựng đủ mọi tab (~20 lần gọi API).
- Exit 0 → in số function / test / case theo từng nhóm F.
- Exit 2 + `UNMAPPED CLASSES` → có service/validator mới chưa có nhóm. Đề xuất nhóm F<n> cho từng class
  (dựa vào feature-list), **chờ người dùng đồng ý**, sửa `requirement_map.json`, chạy lại.
- Exit 2 + `UNRESOLVED TESTS` → đã ghi, nhưng các test đó nằm ở dòng `?<tiền tố>`. Báo danh sách cho người dùng.
- `TRX RESULTS WITHOUT A SOURCE TEST` → trx cũ hơn code; chạy lại bước 2.
- Lỗi giữa chừng (mạng, quota 429 đã thử lại vẫn hỏng) → các tab đã gửi vẫn nằm trên Sheet, có thể còn tab
  `__template__`; chạy lại cùng lệnh là dựng lại đè lên, rồi kiểm tra tab `__template__` đã biến mất.
- Xem trước không đụng Sheet: `--dry-run` (không cần `--sheet` / key).

### 4. Báo cáo
- Link Sheet đã sửa.
- Số function / test / case, số tab đã dựng, số case fail (nếu có).
- Test chưa map được (nếu có).
- Nhắc: Type N/A/B là đoán theo tên test — nên soát lại vài tab trước khi nộp. Bản cũ xem lại được ở
  *File → Version history*.

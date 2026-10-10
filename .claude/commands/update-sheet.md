---
description: Đồng bộ endpoint trong code → doc/api.md → Google Sheet Harmonia_API_Doc
allowed-tools: Read, Grep, Glob, Edit, Bash(git diff:*), Bash(git status:*), Skill, mcp__claude_ai_Google_Sheets__get_spreadsheet, mcp__claude_ai_Google_Sheets__get_values, mcp__claude_ai_Google_Sheets__update_values, mcp__claude_ai_Google_Sheets__append_values, mcp__claude_ai_Google_Sheets__insert_dimension, mcp__claude_ai_Google_Sheets__batch_clear_values, mcp__claude_ai_Google_Sheets__update_spreadsheet
---

Đồng bộ tài liệu API lên Google Sheet cho FE/mobile. Ghi chú thêm của người gọi: $ARGUMENTS

Sheet: **Harmonia_API_Doc** — id `1fWlhDqXfSpFx2rIbqBStKx_mYAZSpH-S982yskZ14Lc`.
Cần connector **Google Sheets** của claude.ai. Không có tool `mcp__claude_ai_Google_Sheets__*` thì
dừng, bảo người dùng bật connector ở claude.ai → Settings → Connectors rồi mở phiên mới.

Trước lần ghi đầu tiên, nạp skill `anthropic-skills:google-workspace` và đọc `references/sheets.md`.

## Cấu trúc sheet

| Tab | Cột | Nguồn |
|---|---|---|
| `Quy ước` | Mục · Giá trị · Ghi chú | Mục 1 của `doc/api.md` |
| `Mã lỗi` | Feature · Endpoint · HTTP · Code · Ý nghĩa | Cột "Mã lỗi" của các tab endpoint + bản dịch ở `doc/error-codes.md` |
| `Lookups (dùng chung)` | 9 cột endpoint | Mục Lookups của `doc/api.md` |
| `F<n>. <tên>` | 9 cột endpoint | Endpoint thuộc feature F<n> của `doc/harmonia-feature-list.md` |

9 cột endpoint: `STT · Function · Method · Route · Quyền · Request body · Response thành công · Mã lỗi · Ghi chú`.

- Tab chia theo **feature F**, không theo mục của `api.md` (vd `GET/PUT /api/member-profiles/me` ở tab F1).
- `Route` có kèm query mẫu (`/api/member-profiles?keyword=&status=&pageNumber=1&pageSize=20`).
- `Quyền`: `Anonymous` · `Đã đăng nhập (mọi role)` · `Role: ChoirDirector` (nhiều role ngăn bằng ` · `).
- `Mã lỗi`: mỗi mã một dòng trong ô, dạng `404 MEMBER_NOT_FOUND`.
- `Function`: tên chức năng + mã UC/FE, vd `Duyệt / từ chối kỹ năng (UC-19 / FE-25)`.
- Tab `Mã lỗi`: cột Feature là `F1`, `F2`…; cột Endpoint là `POST /api/auth/login` (không query).

## Các bước

### 1. Code → `doc/api.md`
Quét `[Route]`, `[Http*]`, `[Authorize]`, `[AllowAnonymous]` trong `src/Harmonia.API/Controllers/`;
đọc request/response DTO và service liên quan khi cần. So với `doc/api.md`.
- Lệch (thiếu endpoint, sai DTO, thiếu query, sai role, thiếu mã lỗi) → sửa `doc/api.md` đúng văn phong
  sẵn có, in `git diff doc/api.md`, rồi **DỪNG chờ người dùng xác nhận** mới sang bước 2.
- Khớp → nói "api.md khớp code" và đi tiếp.

### 2. Đọc sheet
`get_spreadsheet` (field mask `sheets.properties.sheetId`, `sheets.properties.title`) lấy danh sách tab,
rồi `get_values` từng tab cần so.

### 3. Xếp endpoint vào tab
- Endpoint đã có dòng ở tab nào → giữ tab đó.
- Endpoint mới → tab của feature F tương ứng trong `doc/harmonia-feature-list.md`. Không chắc thì hỏi.

### 4. So từng dòng — khoá là `Method` + path (bỏ phần `?query`)
- Có rồi, nội dung đổi → cập nhật `Route`, `Quyền`, `Request body`, `Response thành công`, `Mã lỗi`,
  `Ghi chú`. **Không đè `Function`** nếu ô đã có chữ.
- Chưa có → thêm dòng; `Function` lấy tên chức năng + mã UC/FE từ feature-list / use-case-descriptions.
- Có trên sheet nhưng không còn trong code → **chỉ liệt kê**, không xoá dòng.
- Đánh lại `STT` liên tục từ 1. Dòng thứ tự theo `api.md`.

### 5. Tab F chưa có
Tạo bằng `update_spreadsheet` → `duplicateSheet` từ một tab F sẵn có (giữ header, định dạng, độ rộng cột),
`newSheetName` rút gọn như các tab sẵn có (vd `F8. Phân công Phục vụ`), `insertSheetIndex` sao cho các tab F
theo thứ tự số. Rồi `batch_clear_values` vùng `A2:I` của tab mới trước khi ghi.
Lưu ý: xếp tab theo feature-list, không theo mục `api.md` — vd `song-list-items/.../personnel-requirements`
là UC-24 → F8, không phải F6.

### 6. Tab `Mã lỗi`
Dựng lại toàn bộ từ cột `Mã lỗi` của mọi tab endpoint (thứ tự: tab F theo số, trong tab theo STT),
`Ý nghĩa` lấy từ `doc/error-codes.md`. Xoá vùng dữ liệu cũ (`batch_clear_values`, giữ header) rồi ghi lại.
Mã chưa có bản dịch → để trống `Ý nghĩa` và liệt kê trong báo cáo.

### 7. Tab `Quy ước`
So với mục 1 của `doc/api.md`, chỉ sửa dòng thay đổi, thêm dòng mới ở cuối.

### 8. Kiểm tra và báo cáo
Đọc lại từng tab vừa ghi, đối chiếu với dữ liệu định ghi. Báo theo tab:
`<tab>: +N dòng, sửa M dòng` · danh sách dòng cần xoá tay · mã lỗi thiếu bản dịch.
Kèm link: https://docs.google.com/spreadsheets/d/1fWlhDqXfSpFx2rIbqBStKx_mYAZSpH-S982yskZ14Lc/edit

## Luật ghi

- Connector KHÔNG có `valueInputOption: RAW` — ô được parse như gõ tay. Chuỗi bắt đầu bằng `=` / `+`,
  hoặc trông như ngày (`2026-10-01`) mà phải giữ là chữ → thêm `'` ở đầu. `200`, `404` thành số là chấp nhận được.
- `null` trong `values` = bỏ qua ô, `""` = xoá ô.
- Ô `Method` được tô màu TAY theo từng ô (không có conditional format). Mỗi dòng thêm mới hoặc đổi method
  (kể cả tab vừa `duplicateSheet` — màu bám theo vị trí dòng của tab gốc) → `repeatCell` cột C, `fields: "userEnteredFormat"`:
  nền GET `(0.776, 0.937, 0.808)` · POST `(0.988, 0.894, 0.839)` · PUT `(0.894, 0.875, 0.925)` ·
  DELETE `(1, 0.780, 0.808)` (RGB 0–1); PATCH → đọc màu ô PATCH ở tab F3. Kèm viền SOLID 4 cạnh màu
  `(0.749, 0.749, 0.749)`, chữ đậm `Arial, sans-serif`, `verticalAlignment: TOP`, `wrapStrategy: WRAP`,
  không đặt `horizontalAlignment`.
- Sau `duplicateSheet`: xoá sạch định dạng các dòng dưới dòng dữ liệu cuối (`repeatCell` với `cell: {}`,
  `fields: "userEnteredValue,userEnteredFormat,dataValidation,note"`, bỏ `endRowIndex`) — tab gốc dài hơn
  thì còn sót viền / màu.
- Ghi dòng vào vùng trước đó trống (vd cuối tab `Mã lỗi`) → `copyPaste` `PASTE_FORMAT` từ một dòng dữ liệu
  sẵn có xuống các dòng mới, nếu không chúng thiếu viền và căn lề.
- Đọc lại tab ngay trước khi ghi tab đó (Sheets không có revision guard).
- Không xoá tab, không xoá dòng endpoint, không đổi tên tab sẵn có.
- Không commit. Chỉ sửa `doc/api.md` và sheet.

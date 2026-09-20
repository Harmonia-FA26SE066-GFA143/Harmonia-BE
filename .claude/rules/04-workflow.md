# Workflow — áp dụng cho MỌI thay đổi file

```
READ → SUMMARIZE → PLAN → [chờ duyệt] → IMPLEMENT → VERIFY → STOP
```

## Áp dụng tới đâu

| Loại việc | Làm gì |
|---|---|
| Trả lời câu hỏi, đọc hiểu, giải thích code | Không áp dụng. Trả lời thẳng |
| Sửa nhỏ và rõ ràng — đổi chữ, thêm property, sửa typo | READ → IMPLEMENT → VERIFY. Bỏ PLAN |
| Sửa bug, refactor, thêm tính năng, đổi schema, đổi quyền | Đủ sáu giai đoạn |

Không chắc thuộc loại nào thì chọn loại nặng hơn.

## READ

Đọc trước khi nói. Phiên mới không nhớ gì phiên trước.

- Mở file sẽ sửa, đọc nội dung thật — nhiều file trong repo còn 0 byte.
- Grep mọi chỗ gọi tới thứ sắp sửa. Đây là bước hay bị bỏ nhất và là nguyên nhân
  của gần hết lỗi "sửa chỗ này vỡ chỗ kia".
- Đừng hỏi người dùng những gì repo trả lời được.

## SUMMARIZE

Tóm tắt ngắn để người dùng bắt lỗi hiểu sai sớm: vấn đề là gì, nó nằm ở đâu,
những chỗ nào đang phụ thuộc vào nó.

## PLAN

Ba phần, rồi **DỪNG CHỜ DUYỆT**. Không sửa file nào ở giai đoạn này.

- **Câu hỏi** — chỗ chưa rõ, đánh số, hỏi gọn.
- **Đề xuất** — thấy cách tốt hơn thì nêu kèm lý do một dòng, chờ đồng ý.
  Không tự ý làm theo ý mình, cũng không im lặng làm theo cách dở hơn.
- **Danh sách file** — tạo mới cái nào, sửa cái nào, mỗi dòng một câu mô tả.
  Cảnh báo nếu đụng schema (cần migration) hoặc đụng thứ tính năng khác đang dùng.

## IMPLEMENT

Làm đúng theo PLAN. Phát hiện điều gì làm PLAN không còn đúng thì dừng, báo, sửa PLAN
trước — đừng lặng lẽ đi chệch.

### Sửa bug: chữa gốc, không vá triệu chứng

Báo cáo lỗi chỉ nêu triệu chứng. Trước khi sửa, grep mọi chỗ gọi hàm sắp đụng tới.
Thường một guard trong hàm dùng chung sẽ ngắn hơn là guard ở từng nơi gọi — và vá
đúng một đường đi mà báo cáo nhắc tới thì mọi đường còn lại vẫn hỏng.

### Refactor

Không đổi hành vi. Đổi hành vi thì không còn là refactor — tách thành việc riêng và
nói rõ. Không nhân tiện sửa thêm thứ ngoài phạm vi.

### Đổi schema

Property mới, đổi kiểu, đổi quan hệ đều cần migration. Báo, không tự chạy `dotnet ef`.
Nêu rõ tính năng nào đang dùng entity đó và có vỡ không.

## VERIFY

- `dotnet build Harmonia.Solution.slnx`, và `dotnet test` nếu có test.
- Đối chiếu file thực tế với PLAN, lệch chỗ nào nói chỗ đó.
- Tự soát theo `01-layer-boundaries.md` và `03-security.md` ở những file vừa đụng.

## STOP

Không commit. Không chạy `dotnet ef`. Báo lại: file đã tạo/sửa, việc còn lại cần
làm tay, và những gì đã cố ý bỏ qua kèm lý do.
---
description: Soạn báo cáo tuần SEP490 từ git log và ghi chú bổ sung
argument-hint: [số tuần] [ghi chú việc ngoài code] — bỏ trống thì lấy 7 ngày gần nhất
allowed-tools: Bash(git log:*), Bash(git shortlog:*), Read, Write
---

Soạn báo cáo tuần cho đồ án Harmonia. Đầu vào: $ARGUMENTS

## Lấy dữ liệu

```
git log --all --no-merges --since="7 days ago" --date=short \
        --numstat --pretty=format:"%h|%an|%ad|%s"
```

`--all` để không sót feature branch chưa merge. `--no-merges` để merge commit
không nhân đôi số liệu.

Nếu $ARGUMENTS có số tuần cụ thể, hỏi tôi khoảng ngày rồi thay bằng
`--since=<từ> --until=<đến>`.

## Việc ngoài git — BẮT BUỘC hỏi

Repo này chỉ chứa backend. Trước khi viết, hỏi tôi một câu duy nhất:
"Tuần này có việc gì ngoài code backend không?" — tài liệu SRS, use case,
diagram, thiết kế UI, mobile app, họp với thầy hướng dẫn.

$ARGUMENTS có sẵn ghi chú thì dùng luôn, khỏi hỏi.

Không hỏi bước này thì báo cáo sẽ thiếu nửa công việc của nhóm.

## Gom theo người

Nhóm 4 người: Nguyễn Đức Anh, Đào Ngọc Hoàng, Lưu Sa Trường, Nguyễn Huy Hoàng.
Tên trong git có thể viết tắt hoặc không dấu — tự khớp, không chắc thì hỏi.

## Khung xuất

### Báo cáo tuần <n> — <từ ngày> đến <đến ngày>

**Đã làm**
Mỗi người một mục. Viết bằng ngôn ngữ nghiệp vụ, không dán message commit thô —
"hoàn thành API duyệt kỹ năng thành viên" chứ không phải "fix bug, update code".
Gom commit nhỏ cùng chủ đề thành một dòng. Việc ngoài code ghi chung mục của người đó.

**Số liệu**
Số commit, số file đổi, số dòng thêm/xoá của cả nhóm. Ghi rõ "chỉ tính repo backend".

**Tuần tới**
Để trống, tôi tự điền.

**Khó khăn / cần hỗ trợ**
Để trống, tôi tự điền.

## Lưu

Ghi vào `docs/reports/tuan-<n>.md`, đồng thời in ra chat để tôi soát trước khi nộp.

Không bịa việc không có trong git log hoặc trong ghi chú tôi cung cấp.
Tuần không ai commit thì nói thẳng là không có commit, đừng viết lấp chỗ.
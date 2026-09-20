# Quy ước commit — Harmonia

Dạng: `<loại>(<phạm vi>): <mô tả>`

Một dòng, dưới 72 ký tự, không dấu chấm cuối. Tiền tố tiếng Anh, mô tả tiếng Việt.

## Loại

| Loại | Dùng khi |
|---|---|
| `feat` | Thêm chức năng người dùng thấy được |
| `fix` | Sửa lỗi |
| `refactor` | Đổi cấu trúc, KHÔNG đổi hành vi |
| `chore` | Cấu hình, package, công cụ, dọn dẹp |
| `docs` | Tài liệu, comment, file hướng dẫn |
| `test` | Thêm hoặc sửa test |

## Phạm vi

Tên module viết kebab-case, lấy theo từ điển nghiệp vụ trong `.claude/rules/02-naming.md`:

`auth`, `member`, `member-skill`, `choir`, `liturgical-event`, `song-list`,
`song`, `roster`, `rehearsal`, `practice`, `attendance`, `notification`,
`report`, `admin`, `db`, `ci`

Thay đổi trải nhiều module thì bỏ phạm vi: `chore: nâng .NET 9 lên bản vá mới`

## Ví dụ đúng

```
feat(member-skill): thêm API ca trưởng duyệt kỹ năng thành viên
feat(roster): gợi ý phân công dựa trên kỹ năng đã duyệt
fix(song-list): chặn cha xứ duyệt danh sách của giáo xứ khác
fix(auth): sửa token hết hạn không trả 401
refactor(notification): tách SignalR ra khỏi service nghiệp vụ
chore(db): thêm migration cho bảng ServiceRoster
chore(ci): thêm workflow build và deploy
docs: cập nhật quy ước đặt tên trong rules
test(member-skill): thêm case duyệt sai ca đoàn
```

## Ví dụ sai

```
update code                  → không biết đổi gì
fix bug                      → bug nào?
feat: xong phần của tôi      → phần nào?
feat(member-skill): thêm API duyệt kỹ năng và sửa lỗi login và đổi tên bảng
                             → ba việc, tách thành ba commit
```

## Ba nguyên tắc

**Viết cái gì đổi về nghiệp vụ, không phải file nào bị đụng.**
"thêm API duyệt kỹ năng" chứ không phải "sửa MemberSkillService.cs".

**Một commit một việc.** Gộp ba chức năng vào một commit thì không revert riêng được,
và báo cáo tuần sẽ đọc thành một dòng vô nghĩa.

**Thân commit trả lời VÌ SAO, không phải CÁI GÌ.** Cái gì đã nằm trong diff rồi.
Chỉ viết thân khi lý do không hiển nhiên.

## Lưu ý

`git log` là nguồn dữ liệu của `/weekly-report`. Message mơ hồ thì báo cáo tuần
phải viết tay lại từ đầu.
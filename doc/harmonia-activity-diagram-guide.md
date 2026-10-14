# Hướng dẫn vẽ Activity Diagram cho Harmonia

Tài liệu này chỉ cách vẽ activity diagram (UML Activity) cho các use case của Harmonia,
kèm sơ đồ mẫu đã đối chiếu với code ngày 2026-10-12.
Đi cùng `harmonia-state-diagram-guide.md`: state diagram trả lời "một bản ghi đi qua
những trạng thái nào", activity diagram trả lời "ai làm bước gì, theo thứ tự nào".

Sơ đồ nộp báo cáo thì **vẽ tay** (draw.io, Astah, Visual Paradigm hoặc giấy) theo mục 3.
Các khối Mermaid ở mục 4 chỉ là **hình mẫu để nhìn theo** — mở Markdown Preview trong
VS Code hoặc dán vào <https://mermaid.live> để xem hình. Mermaid không có activity diagram
thật nên hình mẫu dùng khung chữ nhật thay swimlane và ô chữ "song song" thay thanh
fork/join; khi vẽ tay phải dùng đúng ký hiệu UML ở bảng dưới.

## 1. Use case nào cần activity diagram

Chỉ vẽ cho use case có **rẽ nhánh** hoặc **nhiều actor chuyền việc cho nhau**.
Use case chỉ xem dữ liệu (UC-01, UC-02, UC-04, UC-06, UC-14, UC-16, UC-18, UC-35...) không cần.

| Sơ đồ | Use case | Vì sao đáng vẽ |
|---|---|---|
| Chuẩn bị một sự kiện (tổng) | UC-12 → UC-22 → UC-13 → UC-24…UC-27 | Ba actor chuyền việc, là luồng chính của hệ thống |
| Đề xuất và duyệt danh sách bài hát | UC-22, UC-13 | Vòng lặp sửa – duyệt, có phiên bản |
| Gợi ý và chốt phân công | UC-25, UC-25a, UC-25b, UC-26, UC-27 | Nhiều điều kiện, có AI và luật dự phòng |
| Nộp và chấm bài tập | UC-09, UC-10, UC-29 | Vòng lặp nộp lại, có hạn nộp |
| Khai và duyệt kỹ năng | UC-03, UC-19 | Hai actor, hai kết cục |
| Quên mật khẩu | S-03 | Luồng bảo mật, có token hết hạn |
| Tạo tài khoản | UC-31 | Sinh mật khẩu, gửi mail, bắt đổi mật khẩu |

## 2. Quy trình vẽ một sơ đồ

1. **Lấy phạm vi** từ `doc/harmonia-use-case-descriptions.md`: mã use case, actor,
   điểm bắt đầu và kết thúc.
2. **Xác định swimlane**: một lane cho mỗi actor tham gia, thêm một lane `Hệ thống`.
   Tên lane dùng đúng tên role: `ParishPriest`, `ChoirDirector`, `ChoirMember`, `Admin`.
3. **Liệt kê hành động của actor**: mỗi lời gọi API là một hành động — tra route ở
   `doc/api.md` hoặc `src/Harmonia.API/Controllers/`.
4. **Liệt kê hành động của hệ thống**: mở method service tương ứng trong
   `src/Harmonia.Application/Services/`, đọc từ trên xuống. Mỗi khối việc (kiểm tra,
   lưu, gửi thông báo) là một hành động.
5. **Tìm nút quyết định**: mỗi `if (...) return Result.Failure(ErrorCodes.X)` là một
   hình thoi. Gộp các guard cùng tính chất (ví dụ ba kiểm tra về sự kiện) thành một hình thoi.
6. **Tìm việc song song**: thông báo gửi cho nhiều người, hoặc hai actor làm việc độc lập
   (ca viên xác nhận tham gia trong khi ca trưởng soạn danh sách).
7. **Vẽ luồng chính trước** (happy path) từ trên xuống, sau đó mới thêm nhánh lỗi.
8. **Đối chiếu ngược**: mỗi hình thoi chỉ được tới một guard trong code, mỗi hành động
   của hệ thống chỉ được tới một đoạn trong service.

## 3. Cách vẽ tay

### Hình cần dùng

| Phần tử | Hình vẽ | Trong hình mẫu Mermaid |
|---|---|---|
| Nút bắt đầu | Chấm tròn đen đặc — đúng một nút | Vòng tròn nhỏ |
| Nút kết thúc | Chấm đen nằm trong vòng tròn — có thể nhiều nút | Vòng tròn kép |
| Hành động | Hình chữ nhật **bo góc**, ghi động từ + bổ ngữ ("Gửi duyệt") | Hình chữ nhật |
| Quyết định | Hình thoi, **một** mũi tên vào, nhiều mũi tên ra; mỗi nhánh ra ghi `[Có]` / `[Không]` | Hình thoi |
| Gộp nhánh (merge) | Hình thoi, nhiều mũi tên vào, một mũi tên ra, không ghi chữ | Mũi tên chụm vào cùng một ô |
| Fork | Thanh đen đậm, một mũi tên vào, nhiều mũi tên ra | Ô nghiêng "song song" |
| Join | Thanh đen đậm, nhiều mũi tên vào, một mũi tên ra | Ô nghiêng "chờ cả hai" |
| Luồng điều khiển | Mũi tên liền | Mũi tên |
| Swimlane | Cột dọc có tiêu đề là tên actor, kẻ đường phân cách | Khung chữ nhật |
| Trả lỗi | Hành động "Báo lỗi …" trong lane `Hệ thống`, kèm ghi chú mã lỗi | Ô nghiêng "Trả MÃ_LỖI" |

Trong draw.io: bật thư viện **UML** (More Shapes → UML), swimlane lấy ở `Vertical Pool` /
`Vertical Swimlane`, thanh fork/join là hình `Fork/Join`.

### Thứ tự vẽ

1. Viết luồng ra giấy dạng danh sách đánh số trước: `ai – làm gì`, mỗi dòng một hành động,
   đánh dấu chỗ rẽ nhánh và chỗ quay lại. Danh sách đúng thì sơ đồ đúng.
2. Kẻ swimlane: actor khởi đầu ở cột trái, `Hệ thống` ở giữa, actor còn lại bên phải.
3. Đặt nút bắt đầu ở đầu lane của actor khởi đầu.
4. Đặt các hành động của **luồng chính** từ trên xuống, mỗi hành động vào đúng lane.
   Thời gian chạy từ trên xuống — hành động xảy ra sau thì nằm thấp hơn.
5. Chèn hình thoi vào chỗ rẽ nhánh. Nhánh "thành công" đi thẳng xuống, nhánh lỗi rẽ ngang.
6. Vẽ nhánh lỗi: kết thúc tại một nút kết thúc riêng, hoặc quay ngược lên hành động cho
   người dùng làm lại.
7. Chèn thanh fork/join cho việc song song. Fork bao nhiêu nhánh thì join bấy nhiêu nhánh.
8. Nối mũi tên, ghi nhãn cho mọi nhánh ra của hình thoi, đặt nút kết thúc ở dưới cùng.

### Bố cục

- Mũi tên đi dọc và ngang, bẻ góc vuông; hạn chế mũi tên chéo và mũi tên cắt nhau.
- Mũi tên quay lại (vòng lặp) đi vòng bên ngoài, không xuyên qua các hành động.
- Một sơ đồ vừa một trang A4 dọc. Tiêu đề: `Activity Diagram – <mã UC> <tên use case>`.

### Quy ước nội dung

- Tên entity, trạng thái, role, mã lỗi giữ nguyên tiếng Anh như trong code.
- Một sơ đồ tối đa khoảng 15 hành động. Dài hơn thì tách: sơ đồ tổng gọi tới sơ đồ con.
- Nhánh lỗi chỉ vẽ những lỗi **nghiệp vụ** người dùng gặp thật. Không vẽ 401, 500,
  lỗi validate từng trường.
- Hành động của hệ thống viết theo việc nó làm ("Lưu phiên bản mới"), không chép tên method.

## 4. Sơ đồ mẫu

### 4.1 Chuẩn bị một sự kiện — sơ đồ tổng

```mermaid
flowchart TD
    S(( )) --> A1

    subgraph PP[ParishPriest]
        A1[Tạo LiturgicalEvent] --> A2[Publish sự kiện]
        C1[Duyệt SongList]
    end

    subgraph CD[ChoirDirector]
        B1[Soạn và gửi duyệt SongList]
        B2[Gửi yêu cầu xác nhận tham gia]
        B3[Khai yêu cầu nhân sự cho từng bài]
        B4[Xin gợi ý phân công, chỉnh tay]
        B5[Chốt ServiceRoster]
        B6[Gửi thông báo phân công]
    end

    subgraph CM[ChoirMember]
        M1[Xác nhận tham gia]
        M2[Xem phân công, tài liệu, tập hát]
    end

    A2 --> F1[/ song song /]
    F1 --> B1
    F1 --> B2
    B2 --> M1
    B1 --> C1
    C1 --> D1{Quyết định?}
    D1 -- Reject / RequestRevision --> B1
    D1 -- Approve --> B3
    B3 --> J1[/ chờ cả hai /]
    M1 --> J1
    J1 --> B4 --> B5 --> B6 --> M2 --> E(((" ")))
```

### 4.2 Đề xuất và duyệt danh sách bài hát — UC-22, UC-13

```mermaid
flowchart TD
    S(( )) --> A1

    subgraph CD[ChoirDirector]
        A1[Chọn sự kiện, xếp bài vào các LiturgicalSlot]
        A2[Sửa các bài trong bản Draft]
        A3[Gửi duyệt]
    end

    subgraph SYS[Hệ thống]
        D1{Sự kiện Published?}
        D2{Phiên bản mới nhất?}
        B1[Tạo SongList Draft, Version + 1]
        D3{Có ít nhất 1 bài?}
        B2[Chuyển Submitted, báo ParishPriest]
        B3[Lưu SongListReview, đổi trạng thái, báo người đề xuất]
        X1[/Trả LITURGICAL_EVENT_NOT_PUBLISHED hoặc LITURGICAL_EVENT_CANCELLED/]
        X2[/Trả SONG_LIST_EVENT_HAS_APPROVED_VERSION/]
        X3[/Trả SONG_LIST_CANNOT_BE_REVISED/]
        X4[/Trả SONG_LIST_EMPTY/]
    end

    subgraph PP[ParishPriest]
        C1[Xem danh sách chờ duyệt]
        C2[Chọn Approve / Reject / RequestRevision, ghi chú]
    end

    A1 --> D1
    D1 -- Không --> X1 --> E1(((" ")))
    D1 -- Có --> D2
    D2 -- Đã Approved --> X2 --> E1
    D2 -- Còn Draft / Submitted --> X3 --> E1
    D2 -- Chưa có, Rejected, NeedsRevision --> B1
    B1 --> A2 --> A3 --> D3
    D3 -- Không --> X4 --> A2
    D3 -- Có --> B2 --> C1 --> C2 --> B3 --> D4{Quyết định?}
    D4 -- Approve --> E2(((" ")))
    D4 -- Reject / RequestRevision --> A1
```

Ghi chú bắt buộc là "ghi chú" ở C2: phải có khi Reject hoặc RequestRevision.

### 4.3 Gợi ý và chốt phân công — UC-25, UC-25a, UC-25b, UC-26, UC-27

```mermaid
flowchart TD
    S(( )) --> A1

    subgraph CD[ChoirDirector]
        A1[Xin gợi ý phân công cho sự kiện]
        A2[Xem gợi ý và cảnh báo thiếu người]
        A3[Thêm / thay / gỡ phân công]
        A4[Chốt roster]
        A5[Chọn ca viên, gửi thông báo]
    end

    subgraph SYS[Hệ thống]
        D1{Sự kiện chưa huỷ, chưa qua?}
        D2{Có SongList Approved và yêu cầu nhân sự?}
        D3{Roster đã Finalized?}
        B1[Lập các vị trí cần người từ SongPersonnelRequirement]
        B2[Chọn người bằng AI]
        B3[Lấp chỗ còn trống theo luật]
        B4[Xoá gợi ý cũ, lưu gợi ý mới, roster = Suggested]
        B5[Tính RosterShortage]
        D4{Mọi phân công còn hợp lệ?}
        B6[Roster = Finalized]
        B7[Gửi thông báo cho từng ca viên]
        X1[/Trả LITURGICAL_EVENT_CANCELLED hoặc LITURGICAL_EVENT_ALREADY_PASSED/]
        X2[/Trả ROSTER_SONG_LIST_NOT_APPROVED hoặc ROSTER_NO_PERSONNEL_REQUIREMENT/]
        X3[/Trả ROSTER_ALREADY_FINALIZED/]
        X4[/Trả MEMBER_NOT_ACTIVE, ROSTER_ASSIGNMENT_MEMBER_SKILL_NOT_APPROVED hoặc ROSTER_ASSIGNMENT_MEMBER_NOT_CONFIRMED/]
    end

    subgraph CM[ChoirMember]
        M1[Nhận thông báo phân công]
    end

    A1 --> D1
    D1 -- Không --> X1 --> E1(((" ")))
    D1 -- Có --> D2
    D2 -- Không --> X2 --> E1
    D2 -- Có --> D3
    D3 -- Có --> X3 --> E1
    D3 -- Không --> B1 --> B2 --> B3 --> B4 --> B5 --> A2
    A2 --> D5{Cần chỉnh?}
    D5 -- Có --> A3 --> A2
    D5 -- Không --> A4 --> D4
    D4 -- Không --> X4 --> A3
    D4 -- Có --> B6 --> A5 --> B7 --> M1 --> E2(((" ")))
```

Ứng viên hợp lệ: `MemberProfile` Active, có `MemberSkill` Approved đúng kỹ năng,
`EventParticipation` Confirmed. Phân công thêm tay được giữ lại khi xin gợi ý lần nữa.

### 4.4 Nộp và chấm bài tập — UC-09, UC-10, UC-29

```mermaid
flowchart TD
    S(( )) --> A1

    subgraph CD[ChoirDirector]
        A1[Tạo PracticeAssignment: bài, đối tượng, hạn nộp]
        A2[Nghe bản thu]
        A3[Chấm Passed / NeedsRevision, viết nhận xét]
    end

    subgraph SYS[Hệ thống]
        B1[Báo ca viên được giao]
        D1{Lần nộp mới nhất đã Passed?}
        D2{Còn hạn, hoặc cho nộp trễ?}
        D3{File .mp3 / .m4a / .wav hợp lệ?}
        B2[Lưu file, tạo PracticeSubmission, AttemptNo + 1]
        B3[Lưu PracticeFeedback, đổi trạng thái, báo ca viên]
        X1[/Trả PRACTICE_SUBMISSION_ALREADY_PASSED/]
        X2[/Trả PRACTICE_SUBMISSION_PAST_DUE/]
        X3[/Trả lỗi file/]
    end

    subgraph CM[ChoirMember]
        M1[Mở bài tập, thu âm]
        M2[Nộp bản thu]
        M3[Xem kết quả và nhận xét]
    end

    A1 --> B1 --> M1 --> M2 --> D1
    D1 -- Có --> X1 --> E1(((" ")))
    D1 -- Không --> D2
    D2 -- Không --> X2 --> E1
    D2 -- Có --> D3
    D3 -- Không --> X3 --> M1
    D3 -- Có --> B2 --> A2 --> A3 --> B3 --> M3 --> D4{Kết quả?}
    D4 -- Passed --> E2(((" ")))
    D4 -- NeedsRevision --> M1
```

### 4.5 Quên mật khẩu — S-03

```mermaid
flowchart TD
    S(( )) --> A1

    subgraph U[Người dùng]
        A1[Nhập email ở màn quên mật khẩu]
        A2[Mở link trong email]
        A3[Nhập mật khẩu mới]
    end

    subgraph SYS[Hệ thống]
        D1{Email có tài khoản đang hoạt động?}
        B1[Xoá token chưa dùng cũ, tạo token mới hạn 1 giờ, lưu hash]
        B2[Gửi email chứa link theo Platform]
        B3[Trả 204]
        D2{Token đúng, còn hạn, chưa dùng?}
        B4[Đổi mật khẩu, đánh dấu token đã dùng]
        B5[Thu hồi mọi RefreshToken]
        X1[/Trả lỗi token: AUTH_RESET_TOKEN_INVALID khi không tìm thấy, mã riêng khi hết hạn hoặc đã dùng — tra doc/error-codes.md/]
    end

    A1 --> D1
    D1 -- Có --> B1 --> B2 --> B3
    D1 -- Không --> B3
    B3 --> A2 --> A3 --> D2
    D2 -- Không --> X1 --> E1(((" ")))
    D2 -- Có --> B4 --> B5 --> E2(((" ")))
```

Cả hai nhánh của D1 đều ra cùng một phản hồi 204 — phải vẽ rõ điều này, vì đó là yêu cầu
bảo mật (không lộ email nào đã đăng ký).

## 5. Checklist trước khi nộp sơ đồ

- [ ] Tiêu đề có mã use case, khớp `doc/harmonia-use-case-descriptions.md`.
- [ ] Đúng một nút bắt đầu; mọi đường đi đều tới một nút kết thúc.
- [ ] Mỗi hành động nằm trong đúng lane của người thực hiện.
- [ ] Mỗi hình thoi là một câu hỏi, mọi nhánh ra đều có nhãn.
- [ ] Mỗi nhánh lỗi ghi đúng mã trong `doc/error-codes.md`.
- [ ] Tên lane là tên role trong code; không có `ChoirLeader`, `Instrumentalist`.
- [ ] Trạng thái nhắc tới trong sơ đồ khớp với state diagram của entity đó.
- [ ] Code đổi luồng thì sửa sơ đồ trong cùng PR.

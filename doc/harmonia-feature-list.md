# Harmonia – Danh sách Feature & Function

Hệ thống Harmonia gồm **12 feature**, bao phủ toàn bộ 54 function (FE-01 → FE-54) cùng 5 use case dùng chung (S-01 → S-05). Mỗi function ghi kèm mã FE/UC để truy vết với Report 1–3. Mã UC theo đánh số chuẩn, giống `harmonia-use-case-descriptions.md`.

| Mã | Actor | Nền tảng |
| --- | --- | --- |
| CM | Choir Member (thành viên ca đoàn) | Mobile App |
| PP | Parish Priest / Liturgy Committee (linh mục / ban phụng vụ) | Web |
| CD | Choir Director (ca trưởng) | Web |
| AD | Admin | Web |

| # | Feature | Số function |
| --- | --- | --- |
| F1 | Xác thực & Tài khoản cá nhân | 4 |
| F2 | Quản lý Người dùng & Phân quyền | 3 |
| F3 | Quản lý Thành viên & Kỹ năng | 5 |
| F4 | Lịch Phụng vụ theo Ngày | 4 |
| F5 | Thư viện Thánh nhạc | 6 |
| F6 | Danh sách Bài hát cho Sự kiện (Song List) | 7 |
| F7 | Lịch tập & Xác nhận Tham gia | 4 |
| F8 | Phân công Phục vụ (Service Roster) | 6 |
| F9 | Bài tập Luyện tập & Chấm điểm | 8 |
| F10 | Điểm danh & Theo dõi Chuẩn bị | 4 |
| F11 | Thông báo & Trao đổi | 3 |
| F12 | Báo cáo, Nhật ký & Cấu hình Hệ thống | 7 |

## F1. Xác thực & Tài khoản cá nhân

| Function | Actor | Mã |
| --- | --- | --- |
| Đăng nhập | Tất cả | S-01 / FE-01 |
| Đăng xuất | Tất cả | S-02 |
| Đổi mật khẩu / Quên mật khẩu | Tất cả | S-03 |
| Xem hồ sơ cá nhân | CM | UC-01 / FE-01 |

## F2. Quản lý Người dùng & Phân quyền

| Function | Actor | Mã |
| --- | --- | --- |
| Tạo / sửa / khóa tài khoản người dùng | AD | UC-31 / FE-47 |
| Quản lý hồ sơ ca viên (thông tin, trạng thái Active / Inactive / Left) | AD | UC-31 / FE-47 |
| Gán vai trò hệ thống (4 vai trò: PP, CD, CM, AD) | AD | UC-31 / FE-48 |

## F3. Quản lý Thành viên & Kỹ năng

| Function | Actor | Mã |
| --- | --- | --- |
| Xem danh sách ca viên cùng kỹ năng đã duyệt và trạng thái (căn cứ để xếp ca viên vào bài hát) | CD | UC-18 / FE-24 |
| Xem vai trò và các kỹ năng đã được duyệt | CM | UC-02 / FE-02 |
| Khai báo kỹ năng mới (bè hát, nhạc cụ, hát solo, xướng đáp ca…) | CM | UC-03 / FE-03 |
| Theo dõi trạng thái duyệt kỹ năng | CM | UC-03 / FE-04 |
| Duyệt / từ chối kỹ năng thành viên khai báo | CD | UC-19 / FE-25 |

## F4. Lịch Phụng vụ theo Ngày

| Function | Actor | Mã |
| --- | --- | --- |
| Xem lịch phụng vụ theo ngày (tên ngày lễ, bậc lễ, mùa phụng vụ lấy từ API lịch Công giáo) | PP | UC-12 / FE-15 |
| Tạo sự kiện cho từng ngày: giờ, loại lễ, nghi thức, địa điểm, yêu cầu đặc biệt (1 ngày có thể có nhiều sự kiện; mùa phụng vụ được gợi ý sẵn từ API) | PP | UC-12 / FE-16 |
| Công bố từng sự kiện, hệ thống tự thông báo cho ca trưởng | PP | UC-12 → S-05 |
| Xem các sự kiện và lịch tập sắp tới | CM | UC-04 / FE-05 |

## F5. Thư viện Thánh nhạc

| Function | Actor | Mã |
| --- | --- | --- |
| Quản lý thư viện bài hát | CD | UC-21 / FE-27 |
| Upload bản nhạc, lời, audio mẫu, tài liệu tập | CD | UC-21 / FE-28 |
| Phân loại bài hát theo mùa phụng vụ, loại lễ, nghi thức, chủ đề, yêu cầu bè / nhạc cụ | CD | UC-21 / FE-29 |
| Truy cập tài liệu nhạc phù hợp với bài / kỹ năng được giao | CM | UC-07 / FE-08 |
| Tìm kiếm / lọc tài liệu | CM | UC-07E |
| Đánh dấu tài liệu "đã thuộc" / "cần tập thêm" | CM | UC-08 / FE-09 |

## F6. Danh sách Bài hát cho Sự kiện (Song List)

| Function | Actor | Mã |
| --- | --- | --- |
| Đề xuất danh sách bài hát cho lễ / sự kiện | CD | UC-22 / FE-30 |
| Gửi danh sách lên linh mục / ban phụng vụ duyệt | CD | UC-22 / FE-31 |
| Chỉnh sửa danh sách theo góp ý | CD | UC-22 / FE-32 |
| Xem danh sách bài hát được đề xuất | PP | UC-13 / FE-17 |
| Duyệt / từ chối / yêu cầu sửa | PP | UC-13 / FE-18 |
| Ghi chú về mức độ phù hợp (chủ đề, mùa phụng vụ, mục vụ) | PP | UC-13 / FE-19 |
| Xem danh sách bài hát đã duyệt cuối cùng | PP, CM | UC-14 / FE-20, UC-06 / FE-07 |

## F7. Lịch tập & Xác nhận Tham gia

| Function | Actor | Mã |
| --- | --- | --- |
| Lên lịch tập cho từng sự kiện | CD | UC-20 / FE-26 |
| Gửi yêu cầu xác nhận tham gia cho thành viên | CD | UC-23 / FE-33 |
| Xem trạng thái xác nhận theo thời gian thực | CD | UC-23 / FE-34 |
| Xác nhận tham gia / từ chối / chưa chắc | CM | UC-05 / FE-06 |

## F8. Phân công Phục vụ (Service Roster)

Đây chính là phần "quản lý ca viên" của ca trưởng: xếp ca viên vào từng bài hát của sự kiện.

| Function | Actor | Mã |
| --- | --- | --- |
| Định nghĩa nhân sự cần cho mỗi bài (số người mỗi bè, số người mỗi nhạc cụ) | CD | UC-24 / FE-35 |
| Yêu cầu hệ thống gợi ý danh sách phân công | CD | UC-25 / FE-36 |
| Xem cảnh báo thiếu bè / nhạc cụ / vai trò | CD | UC-25a / FE-37 |
| Điều chỉnh phân công thủ công | CD | UC-25b / FE-38 |
| Chốt phân công (sau khi song list đã được duyệt) | CD | UC-26 / FE-39 |
| Gửi thông báo phân công cho thành viên được chọn | CD | UC-27 / FE-40 |

## F9. Bài tập Luyện tập & Chấm điểm

| Function | Actor | Mã |
| --- | --- | --- |
| Tạo bài tập cho toàn đoàn, nhóm kỹ năng hoặc cá nhân | CD | UC-28 / FE-41 |
| Nhận bài tập | CM | UC-09 / FE-10 |
| Thu âm và nộp audio luyện tập qua app | CM | UC-09 / FE-11 |
| Nghe audio thành viên nộp | CD | UC-29 / FE-42 |
| Đánh giá Passed / Needs Revision | CD | UC-29 / FE-43 |
| Nhập nhận xét cho từng bài nộp | CD | UC-29 / FE-44 |
| Xem trạng thái bài nộp (Submitted / Passed / Needs Revision / Overdue) | CM | UC-10 / FE-12 |
| Xem nhận xét của ca trưởng | CM | UC-10 / FE-13 |

## F10. Điểm danh & Theo dõi Chuẩn bị

| Function | Actor | Mã |
| --- | --- | --- |
| Điểm danh buổi tập / buổi chuẩn bị | CD | UC-30 / FE-45 |
| Theo dõi tiến độ luyện tập và chuyên cần cho sự kiện sắp tới | CD | UC-30 / FE-46 |
| Theo dõi tình trạng chuẩn bị của ca đoàn cho sự kiện quan trọng | PP | UC-15 / FE-21 |
| Xem lịch sử tham gia và luyện tập cá nhân | CM | UC-11 / FE-14 |

## F11. Thông báo & Trao đổi

| Function | Actor | Mã |
| --- | --- | --- |
| Gửi thông báo (dùng chung, được gọi từ F4, F7, F8 và UC-17) | Hệ thống | S-05 |
| Xem thông báo | Tất cả | S-04 |
| Gửi ghi chú / yêu cầu cho ca trưởng (gắn với một ngày hoặc một sự kiện) | PP | UC-17 / FE-23 |

## F12. Báo cáo, Nhật ký & Cấu hình Hệ thống

| Function | Actor | Mã |
| --- | --- | --- |
| Báo cáo lịch sử phục vụ, tần suất dùng bài hát, chuẩn bị sự kiện | PP | UC-16 / FE-22 |
| Báo cáo hoạt động người dùng, điểm danh, xác nhận, hoàn thành bài tập | AD | UC-34 / FE-52 |
| Xuất báo cáo theo tháng / sự kiện / mùa phụng vụ | AD | UC-34E / FE-53 |
| Xem lịch sử hoạt động quan trọng (Audit Log) | AD | UC-35 / FE-54 |
| Cấu hình danh mục kỹ năng | AD | UC-32 / FE-49 |
| Cấu hình mùa phụng vụ, loại lễ, nghi thức, loại sự kiện | AD | UC-32 / FE-50 |
| Quản lý cài đặt chung của hệ thống | AD | UC-33 / FE-51 |

## Ghi chú

- Feature được gom theo nghiệp vụ, không theo actor.
- F12 đang gộp nhiều thứ. Nếu dùng làm module để chia việc cho BE, nên tách thành "Báo cáo & Audit" và "Cấu hình hệ thống".
- Hồ sơ ca viên do Admin quản lý (F2). Ca trưởng chỉ xem (F3) và xếp ca viên theo bài hát (F8). Quyết định ngày 2026-09-29.
- Lịch phụng vụ làm theo ngày, không theo tuần; FE-16a (sao chép tuần trước) đã bỏ. Quyết định ngày 2026-09-30.
- Không có vai trò Instrumentalist: người chơi nhạc cụ là Choir Member có kỹ năng nhạc cụ đã được duyệt (F3).

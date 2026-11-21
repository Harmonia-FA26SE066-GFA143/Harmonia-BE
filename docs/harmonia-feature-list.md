# Harmonia – Danh sách Feature & Function

Hệ thống Harmonia gồm **15 feature**, bao phủ toàn bộ 54 function (FE-01 → FE-54), các function ca đoàn (D7, D9), giờ lễ cố định, đăng ký gia nhập, đề cử ca trưởng cùng 5 use case dùng chung (S-01 → S-05). Mỗi function ghi kèm mã FE/UC để truy vết với Report 1–3. Mã UC theo đánh số chuẩn, giống `harmonia-use-case-descriptions.md`. Function làm thêm sau Report 3 chưa có mã FE thì cột Mã chỉ ghi UC, hoặc `—` nếu chưa có UC.

| Mã | Actor | Nền tảng |
| --- | --- | --- |
| CM | Choir Member (thành viên ca đoàn) | Mobile App |
| PP | Parish Priest / Liturgy Committee (linh mục / ban phụng vụ) | Web |
| CD | Choir Director (ca trưởng) | Web |
| AD | Admin | Web |
| KV | Khách (chưa đăng nhập) | Web |

| # | Feature | Số function |
| --- | --- | --- |
| F1 | Xác thực & Tài khoản cá nhân | 6 |
| F2 | Quản lý Người dùng & Phân quyền | 4 |
| F3 | Quản lý Thành viên & Kỹ năng | 8 |
| F4 | Lịch Phụng vụ theo Ngày | 8 |
| F5 | Thư viện Thánh nhạc | 10 |
| F6 | Danh sách Bài hát cho Sự kiện (Song List) | 9 |
| F7 | Lịch tập & Xác nhận Tham gia | 8 |
| F8 | Phân công Phục vụ (Service Roster) | 8 |
| F9 | Bài tập Luyện tập & Chấm điểm | 9 |
| F10 | Điểm danh & Theo dõi Chuẩn bị | 4 |
| F11 | Thông báo & Trao đổi | 4 |
| F12 | Báo cáo & Nhật ký | 8 |
| F13 | Quản lý Ca đoàn | 6 |
| F14 | Cấu hình Hệ thống | 6 |
| F15 | Đề cử & Đào tạo Ca trưởng | 4 |

## F1. Xác thực & Tài khoản cá nhân

| Function | Actor | Mã |
| --- | --- | --- |
| Đăng nhập bằng email / mật khẩu hoặc tài khoản Google (chỉ khớp tài khoản có sẵn) | Tất cả | S-01 / FE-01 |
| Đăng xuất (thiết bị hiện tại / mọi thiết bị) | Tất cả | S-02 |
| Đổi mật khẩu / Quên mật khẩu (bắt buộc đổi ở lần đăng nhập đầu) | Tất cả | S-03 |
| Xem hồ sơ cá nhân | CM | UC-01 / FE-01 |
| Tự sửa họ tên, số điện thoại | Tất cả | — |
| Đổi / xoá ảnh đại diện | Tất cả | — |

## F2. Quản lý Người dùng & Phân quyền

| Function | Actor | Mã |
| --- | --- | --- |
| Tạo / sửa / khóa / mở lại tài khoản người dùng (mật khẩu đầu tiên gửi qua email) | AD | UC-31 / FE-47 |
| Quản lý hồ sơ ca viên (thông tin, trạng thái Active / Inactive / Left) | AD | UC-31 / FE-47 |
| Gán vai trò hệ thống (4 vai trò: PP, CD, CM, AD; không đổi được ca trưởng còn phụ trách ca đoàn) | AD | UC-31 / FE-48 |
| Gửi đơn đăng ký gia nhập từ trang chính; Admin xem đơn, tạo tài khoản từ đơn hoặc xoá đơn | KV, AD | UC-45 |

## F3. Quản lý Thành viên & Kỹ năng

| Function | Actor | Mã |
| --- | --- | --- |
| Xem danh sách ca viên cùng kỹ năng đã duyệt và trạng thái (căn cứ để xếp ca viên vào bài hát) | CD | UC-18 / FE-24 |
| Xem vai trò và các kỹ năng đã được duyệt | CM | UC-02 / FE-02 |
| Khai báo kỹ năng mới (bè hát, nhạc cụ, hát solo, xướng đáp ca…) | CM | UC-03 / FE-03 |
| Nộp minh chứng cho kỹ năng đang chờ duyệt (ghi âm hoặc video) | CM | UC-03 |
| Theo dõi trạng thái duyệt kỹ năng | CM | UC-03 / FE-04 |
| Duyệt / từ chối kỹ năng thành viên khai báo (từng dòng hoặc nhiều dòng một lần) | CD | UC-19 / FE-25 |
| Gán thẳng kỹ năng cho ca viên mình quản lý | CD | UC-19 |
| Thu hồi kỹ năng đã duyệt (gỡ khỏi phân công sắp tới) | CD | UC-19 |

## F4. Lịch Phụng vụ theo Ngày

| Function | Actor | Mã |
| --- | --- | --- |
| Đồng bộ lịch phụng vụ theo năm từ Liturgical Calendar API | PP | UC-12 / FE-15 |
| Xem lịch phụng vụ theo ngày (tên ngày lễ, bậc lễ, mùa phụng vụ, tên lễ đã dịch tiếng Việt) | PP | UC-12 / FE-15 |
| Tạo / sửa sự kiện cho từng ngày: giờ, loại lễ, nghi thức, địa điểm, ca đoàn, yêu cầu đặc biệt (1 ngày có thể có nhiều sự kiện; mùa phụng vụ được gợi ý sẵn) | PP | UC-12 / FE-16 |
| Công bố từng sự kiện, hệ thống tự thông báo cho ca trưởng | PP | UC-12 → S-05 |
| Huỷ sự kiện, thông báo cho người liên quan | PP | UC-12 |
| Xem các sự kiện và lịch tập sắp tới, chi tiết từng sự kiện / buổi tập, phân công của tôi | CM | UC-04 / FE-05 |
| Quản lý giờ lễ cố định của giáo xứ (thứ trong tuần, giờ, loại lễ, địa điểm) | AD | UC-36 |
| Sinh sự kiện nháp từ giờ lễ cố định cho một khoảng ngày | PP | UC-12E |

## F5. Thư viện Thánh nhạc

| Function | Actor | Mã |
| --- | --- | --- |
| Quản lý thư viện bài hát | CD | UC-21 / FE-27 |
| Upload / thay file / xoá bản nhạc, lời, audio mẫu (kể cả MIDI), tài liệu tập, bản nhạc tương tác MusicXML | CD | UC-21 / FE-28 |
| Phân loại bài hát theo mùa phụng vụ, loại lễ, nghi thức, chủ đề, yêu cầu bè / nhạc cụ | CD | UC-21 / FE-29 |
| Soạn lời có mốc thời gian (karaoke); AI trích lời từ bản nhạc | CD | UC-21 |
| Truy cập tài liệu nhạc phù hợp với bài / kỹ năng được giao | CM | UC-07 / FE-08 |
| Tìm kiếm / lọc tài liệu | CM | UC-07E |
| Nghe bản phát MIDI (piano roll) và xem bản nhạc tương tác | CM | UC-07 |
| Hát theo lời karaoke | CM | UC-07 |
| Đánh dấu tài liệu "đã thuộc" / "cần tập thêm" | CM | UC-08 / FE-09 |
| Xem tiến độ học tài liệu của các ca viên mình quản lý | CD | UC-08 |

## F6. Danh sách Bài hát cho Sự kiện (Song List)

| Function | Actor | Mã |
| --- | --- | --- |
| Đề xuất danh sách bài hát cho lễ / sự kiện (chỉ ca đoàn phụ trách) | CD | UC-22 / FE-30 |
| Gợi ý bài phù hợp cho từng vị trí trong lễ | CD | UC-22 |
| Gửi danh sách lên linh mục / ban phụng vụ duyệt | CD | UC-22 / FE-31 |
| Chỉnh sửa danh sách theo góp ý (mỗi lần sửa là một phiên bản mới) | CD | UC-22 / FE-32 |
| Xem danh sách bài hát được đề xuất | PP | UC-13 / FE-17 |
| Duyệt / từ chối / yêu cầu sửa | PP | UC-13 / FE-18 |
| Ghi chú về mức độ phù hợp (chủ đề, mùa phụng vụ, mục vụ) | PP | UC-13 / FE-19 |
| Xem lịch sử các phiên bản danh sách và nhận xét duyệt | PP, CD | UC-13 |
| Xem danh sách bài hát đã duyệt cuối cùng | PP, CM | UC-14 / FE-20, UC-06 / FE-07 |

## F7. Lịch tập & Xác nhận Tham gia

| Function | Actor | Mã |
| --- | --- | --- |
| Lên lịch tập cho từng sự kiện | CD | UC-20 / FE-26 |
| Chọn bài hát cho buổi tập (lấy sẵn từ danh sách đã duyệt của sự kiện) | CD | UC-20 |
| Ghi âm buổi tập, nghe lại / xoá bản thu | CD | UC-20 |
| Gửi yêu cầu xác nhận tham gia cho thành viên | CD | UC-23 / FE-33 |
| Nhắc ca viên chưa trả lời (tay hoặc tự động hằng ngày) | CD | UC-23 |
| Xem trạng thái xác nhận theo thời gian thực | CD | UC-23 / FE-34 |
| Xác nhận hộ toàn bộ lời mời chưa trả lời | CD | UC-23 |
| Xác nhận tham gia / từ chối / chưa chắc | CM | UC-05 / FE-06 |

## F8. Phân công Phục vụ (Service Roster)

Đây chính là phần "quản lý ca viên" của ca trưởng: xếp ca viên vào từng bài hát của sự kiện.

| Function | Actor | Mã |
| --- | --- | --- |
| Định nghĩa nhân sự cần cho mỗi bài (số người mỗi bè, số người mỗi nhạc cụ) và cho cả buổi lễ (organ, bắt nhịp) | CD | UC-24 / FE-35 |
| Yêu cầu hệ thống gợi ý danh sách phân công (AI, dự phòng bằng luật) | CD | UC-25 / FE-36 |
| Xem cảnh báo thiếu bè / nhạc cụ / vai trò | CD | UC-25a / FE-37 |
| Xem cảnh báo ca viên trùng giờ tập ở ca đoàn khác | CD | UC-25a |
| Điều chỉnh phân công thủ công (thêm, thay người, gỡ, ghi chú) | CD | UC-25b / FE-38 |
| Chốt sớm các vai cả buổi lễ | CD | UC-26 |
| Chốt phân công (sau khi song list đã được duyệt) | CD | UC-26 / FE-39 |
| Gửi thông báo phân công cho thành viên được chọn | CD | UC-27 / FE-40 |

## F9. Bài tập Luyện tập & Chấm điểm

| Function | Actor | Mã |
| --- | --- | --- |
| Tạo / sửa / xoá bài tập cho toàn đoàn, nhóm kỹ năng hoặc cá nhân | CD | UC-28 / FE-41 |
| Nhận bài tập | CM | UC-09 / FE-10 |
| Thu âm và nộp audio luyện tập qua app | CM | UC-09 / FE-11 |
| Chấm điểm tự động bản thu (cao độ, nhịp so với bản MIDI của tư liệu) | CM, CD | UC-09 |
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
| Xem thông báo, đếm và đánh dấu đã đọc | Tất cả | S-04 |
| Gửi ghi chú / yêu cầu cho ca trưởng (gắn với một ngày hoặc một sự kiện) | PP | UC-17 / FE-23 |
| Trao đổi (chat) trên từng ghi chú, đánh dấu đã đọc | PP, CD | UC-17 |

## F12. Báo cáo & Nhật ký

| Function | Actor | Mã |
| --- | --- | --- |
| Báo cáo lịch sử phục vụ, tần suất dùng bài hát, chuẩn bị sự kiện | PP | UC-16 / FE-22 |
| Báo cáo hoạt động người dùng, điểm danh, xác nhận, hoàn thành bài tập | AD | UC-34 / FE-52 |
| Báo cáo điểm danh, xác nhận, hoàn thành bài tập của ca viên (lọc được theo ca đoàn) | CD | UC-34 / FE-52 |
| Thống kê theo kỳ (tuần / tháng) | AD, CD | UC-34 |
| Xuất báo cáo theo tháng / sự kiện / mùa phụng vụ | AD | UC-34E / FE-53 |
| Xuất 3 báo cáo theo ca viên | CD | UC-34E / FE-53 |
| Xem lịch sử các lần xuất, tải lại file | AD, CD | UC-34E |
| Xem lịch sử hoạt động quan trọng (Audit Log) | AD | UC-35 / FE-54 |

## F13. Quản lý Ca đoàn

| Function | Actor | Mã |
| --- | --- | --- |
| Quản lý ca đoàn: tạo, sửa, tắt ca đoàn, gán ca trưởng | AD | UC-37 |
| Xem thành viên, rút ca viên khỏi ca đoàn; Admin xếp thẳng / chuyển ca viên, ca trưởng mời ca viên chưa có ca đoàn (mỗi ca viên tối đa một ca đoàn — D9; ca trưởng chỉ ca đoàn của mình) | CD, AD | UC-38 |
| Xin vào / xin chuyển ca đoàn, trả lời lời mời, huỷ yêu cầu, tự rời ca đoàn | CM | UC-47 |
| Duyệt / từ chối yêu cầu vào ca đoàn | CD, AD | UC-48 |
| Gán ca đoàn phục vụ sự kiện và chọn ca đoàn phụ trách (một sự kiện có nhiều ca đoàn) | PP | UC-39 |
| Chỉ định ca trưởng thay thế cho một sự kiện | PP | UC-39 |

## F14. Cấu hình Hệ thống

| Function | Actor | Mã |
| --- | --- | --- |
| Cấu hình danh mục kỹ năng | AD | UC-32 / FE-49 |
| Cấu hình mùa phụng vụ, loại lễ, nghi thức, loại sự kiện, chủ đề bài hát | AD | UC-32 / FE-50 |
| Cấu hình vị trí trong lễ và người luôn cần ở mỗi vị trí, địa điểm cử hành | AD | UC-32 |
| Quản lý bản dịch tiếng Việt tên lễ, bậc lễ, mùa từ API lịch | AD | UC-32 |
| Quản lý cài đặt chung của hệ thống | AD | UC-33 / FE-51 |
| Đọc cài đặt công khai (tên giáo xứ, email liên hệ…) | Tất cả, KV | UC-33 |

## F15. Đề cử & Đào tạo Ca trưởng

| Function | Actor | Mã |
| --- | --- | --- |
| Đề cử ca viên làm ca trưởng, xem cảnh báo nếu chưa đủ ngưỡng thành tích | CD, PP | UC-46 |
| Duyệt / từ chối / huỷ đề cử | PP | UC-46 |
| Cử đi học lớp ca trưởng, ghi kết quả, upload chứng chỉ hoặc miễn đào tạo | PP | UC-46 |
| Bổ nhiệm ca trưởng (đổi role, gán ca đoàn) | AD | UC-46 |

## Ghi chú

- Feature được gom theo nghiệp vụ, không theo actor.
- 2026-10-20: tách F12 cũ thành F12 "Báo cáo & Nhật ký" và F14 "Cấu hình Hệ thống"; gom chức năng ca đoàn (D7) từ F3, F4 về F13; thêm giờ lễ cố định vào F4. F1–F11 giữ số cũ để không lệch các tab trên Google Sheet và tham chiếu trong docs.
- 2026-11-19: đồng bộ với service đã làm — thêm F15 (UC-46), đăng ký gia nhập (UC-45) vào F2, yêu cầu vào / chuyển ca đoàn (UC-47, UC-48) vào F13 và sửa UC-38 theo D9; bổ sung các function phụ đã có trong code (Google login, avatar, minh chứng kỹ năng, karaoke, MIDI, gợi ý bài, bản thu buổi tập, nhắc xác nhận, chấm điểm tự động, chat ghi chú, thống kê theo kỳ…). Function chưa có mã FE không tự đặt mã mới. F1–F14 giữ số cũ.
- Hồ sơ ca viên do Admin quản lý (F2). Ca trưởng chỉ xem (F3) và xếp ca viên theo bài hát (F8). Quyết định ngày 2026-09-29.
- Lịch phụng vụ làm theo ngày, không theo tuần; FE-16a (sao chép tuần trước) đã bỏ. Quyết định ngày 2026-09-30.
- Không có vai trò Instrumentalist: người chơi nhạc cụ là Choir Member có kỹ năng nhạc cụ đã được duyệt (F3).

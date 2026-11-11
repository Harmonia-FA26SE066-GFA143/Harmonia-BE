# Quy trình nghiệp vụ Harmonia

## QUY TRÌNH CA VIÊN → CA TRƯỞNG

### Gia nhập (2 cách)
1. Nộp đơn:
   - Online: đăng ký qua trang chính, gồm họ tên, email, số điện thoại, ca đoàn muốn vào (không bắt buộc), chưa cần đăng nhập
   - Offline: điền đơn giấy tại giáo xứ, đơn được chuyển cho Admin
2. Tạo tài khoản ca viên, server gửi mật khẩu qua email (Admin)
   - Đơn online: Admin xem danh sách đăng ký, bấm tạo tài khoản (thông tin điền sẵn); đăng ký không hợp lệ thì xoá
   - Đơn offline: Admin tạo tài khoản trực tiếp như hiện nay
3. Đăng nhập lần đầu, đổi mật khẩu (Ca viên)
4. Thêm vào ca đoàn, một người có thể ở nhiều ca đoàn (Ca trưởng của ca đoàn đó)
5. Khai báo kỹ năng, nộp minh chứng → ca trưởng duyệt (Ca viên, ca trưởng)
6. Đi tập, làm bài tập, phục vụ lễ: tích lũy thành tích (Ca viên)

### Lên ca trưởng
7. Tạo đề cử, kèm bảng thành tích và cảnh báo nếu chưa đủ ngưỡng (Ca trưởng: ca viên của ca đoàn mình; hoặc cha xứ: ca viên bất kỳ)
8. Duyệt hoặc từ chối đề cử (Cha xứ)
9. Cử đi học lớp ca trưởng, ghi kết quả; được học nhiều lần hoặc được miễn, bắt buộc ghi lý do (Cha xứ)
10. Bổ nhiệm: chọn ca đoàn (theo gợi ý, ca đoàn khác hoặc ca đoàn mới), đổi role sang ca trưởng, gán ca trưởng cho ca đoàn (Admin)
11. Bàn giao tự động (Hệ thống):
    - Người mới đăng nhập lại để nhận quyền ca trưởng
    - Người mới rời các ca đoàn với tư cách ca viên, ca trưởng các ca đoàn đó được báo
    - Ca trưởng cũ được báo, vẫn giữ các ca đoàn khác của mình
    - Buổi tập, danh sách bài, phân công của ca đoàn tự chuyển sang người mới


## QUY TRÌNH THÁNH LỄ CHÚA NHẬT (lễ thường kỳ, một ca đoàn)

### Chuẩn bị lịch
1. Khai báo giờ lễ cố định của giáo xứ, gồm thứ, giờ, loại lễ, địa điểm (Admin)
2. Đồng bộ lịch phụng vụ theo năm từ API. Hệ thống dịch tên lễ, bậc lễ, mùa sang tiếng Việt (Cha xứ)
3. Sinh sự kiện nháp từ giờ lễ cố định cho một khoảng ngày, ví dụ cả tháng (Cha xứ)
   - Hệ thống tự gợi ý mùa phụng vụ
   - Giờ lễ đã có sự kiện thì bỏ qua, không tạo trùng
4. Gán ca đoàn phục vụ cho từng lễ, chọn ca đoàn phụ trách (Cha xứ)
5. Công bố sự kiện, bắt buộc đã có ca đoàn. Hệ thống báo cho ca trưởng (Cha xứ)

### Lập danh sách bài
6. Lập danh sách bài theo vị trí trong lễ: Nhập lễ, Đáp ca, Dâng lễ, Hiệp lễ, Kết lễ (Ca trưởng)
   - Hệ thống gợi ý bài hợp mùa, loại lễ, vị trí; ưu tiên bài lâu chưa hát
   - Được thêm vị trí riêng nếu cần
7. Gửi cha xứ duyệt (Ca trưởng)
8. Duyệt, từ chối hoặc yêu cầu sửa, kèm ghi chú (Cha xứ)
   - Yêu cầu sửa: ca trưởng sửa rồi gửi lại, mỗi lần gửi là một phiên bản mới
9. Ca viên xem được danh sách sau khi đã duyệt (Ca viên)

### Phân công
10. Gửi lời mời xác nhận tham gia (Ca trưởng)
11. Ca viên chọn tham gia, từ chối hoặc chưa chắc (Ca viên)
12. Khai báo nhân sự cần cho từng bài (số người mỗi bè, nhạc cụ) và cho cả buổi lễ (organ, bắt nhịp) (Ca trưởng)
13. Hệ thống gợi ý phân công từ kỹ năng đã duyệt và người đã xác nhận, kèm cảnh báo thiếu người. Ca trưởng chỉnh tay (Ca trưởng)
14. Chốt phân công, chỉ được chốt khi danh sách bài đã duyệt. Gửi thông báo cho người được phân công (Ca trưởng)
15. Phục vụ lễ. Hệ thống ghi lịch sử phục vụ, dùng cho báo cáo và thành tích đề cử ca trưởng


## QUY TRÌNH LỄ GIÁNG SINH (lễ trọng, nhiều ca đoàn, chuẩn bị dài)

Khác lễ Chúa nhật ở chỗ: nhiều thánh lễ trong một ngày, nhiều ca đoàn, phải tập nhiều buổi và cha xứ theo dõi tiến độ.

### Chuẩn bị lịch (từ đầu Mùa Vọng)
1. Tạo tay các sự kiện: Lễ Đêm, Lễ Rạng đông, Lễ Ban ngày, có thể cả nghi thức đặc biệt như diễn nguyện.
   Mỗi sự kiện có giờ, địa điểm, loại lễ, yêu cầu đặc biệt (Cha xứ)
2. Gán nhiều ca đoàn cho mỗi sự kiện, chọn MỘT ca đoàn phụ trách (Cha xứ)
   - Ca đoàn phụ trách lập danh sách bài, mời và phân công
   - Ca đoàn phục vụ khác chỉ xem
3. Công bố từng sự kiện. Hệ thống báo cho ca trưởng các ca đoàn được gán (Cha xứ)
4. Gửi ghi chú, yêu cầu cho ca trưởng gắn với sự kiện, ví dụ "Lễ Đêm hát Kinh Vinh Danh bản Latinh" (Cha xứ)

### Danh sách bài và nhân sự
5. Lập danh sách bài, gửi duyệt, sửa theo góp ý, như bước 6–9 của lễ Chúa nhật (Ca trưởng ca đoàn phụ trách, Cha xứ)
6. Khai báo nhân sự cả buổi lễ (organ, bắt nhịp) và chốt sớm phần này trước khi danh sách bài được duyệt,
   để báo người chơi organ và người bắt nhịp chuẩn bị (Ca trưởng ca đoàn phụ trách)
7. Mời ca viên của mọi ca đoàn phục vụ xác nhận tham gia (Ca trưởng ca đoàn phụ trách)
8. Xác nhận tham gia (Ca viên)

### Tập luyện (xem quy trình luyện tập bên dưới)
9. Lên nhiều buổi tập gắn với sự kiện. Hệ thống điền sẵn bài từ danh sách đã duyệt (Ca trưởng)
10. Giao bài tập về nhà cho bè hoặc người khó (Ca trưởng)

### Theo dõi và chốt
11. Theo dõi tình trạng chuẩn bị: danh sách bài đã duyệt chưa, số người xác nhận, chuyên cần,
    bài tập quá hạn, thiếu người (Cha xứ)
12. Gợi ý phân công, chỉnh tay, chốt, gửi thông báo (Ca trưởng ca đoàn phụ trách)
13. Phát sinh sát ngày:
    - Ca viên rút: thay người trên dòng phân công, giữ lại dòng cũ (Ca trưởng)
    - Ca trưởng vắng đột xuất: cha xứ chỉ định ca trưởng khác thay cho riêng sự kiện này (Cha xứ)
14. Phục vụ lễ. Sau lễ xem báo cáo chuẩn bị sự kiện, lịch sử phục vụ, tần suất dùng bài (Cha xứ, Admin)


## QUY TRÌNH LUYỆN TẬP (buổi tập + bài tập về nhà)

### Buổi tập tại phòng tập
1. Tạo buổi tập: thời gian, địa điểm, ca đoàn, gắn sự kiện (không bắt buộc) (Ca trưởng)
   - Buổi tập chưa gắn ca đoàn thì ca trưởng nào cũng quản lý được
2. Chọn bài cho buổi tập. Nếu gắn sự kiện thì hệ thống gợi ý bài từ danh sách đã duyệt (Ca trưởng)
3. Xem lịch tập sắp tới cùng sự kiện, tài liệu nhạc của bài: bản nhạc, lời, audio mẫu, Karaoke (Ca viên)
4. Điểm danh: có mặt, vắng, trễ, vắng có phép (Ca trưởng)
5. Ghi âm buổi tập trên trình duyệt để ca viên nghe lại (Ca trưởng)

### Bài tập về nhà
6. Giao bài tập: cho cả ca đoàn, một nhóm kỹ năng (ví dụ bè Alto) hoặc cá nhân; kèm tài liệu và hạn nộp (Ca trưởng)
7. Nhận bài, tập theo tài liệu, đánh dấu "đã thuộc" hoặc "cần tập thêm" (Ca viên)
8. Thu âm qua app. Có thể tự chấm thử với bản MIDI mẫu trước khi nộp; bản chấm thử không lưu lại (Ca viên)
9. Nộp bản thu (Ca viên)
10. Nghe bản nộp, đánh giá Đạt / Cần sửa, viết nhận xét (Ca trưởng)
    - Cần sửa: ca viên nộp lại, quay về bước 8
    - Quá hạn chưa nộp: hệ thống tính là quá hạn lúc đọc dữ liệu, không lưu trạng thái này
11. Xem trạng thái và nhận xét, xem lịch sử tập và nộp bài của mình (Ca viên)

### Theo dõi
12. Theo dõi tiến độ luyện tập và chuyên cần cho sự kiện sắp tới (Ca trưởng)
13. Xuất báo cáo điểm danh, xác nhận, hoàn thành bài tập theo ca viên (Ca trưởng, Admin)
14. Chuyên cần và bài tập dồn vào thành tích, dùng khi đề cử ca trưởng (Hệ thống)

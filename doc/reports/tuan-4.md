### Báo cáo tuần 4 — 28/09/2026 đến 04/10/2026

**Đã làm**

*Lưu Sa Trường*
- Hoàn thành đăng nhập bằng Google: xác thực token Google, chỉ khớp tài khoản có sẵn, bổ sung mã lỗi tương ứng.
- Bổ sung họ tên vào phản hồi đăng nhập, chuyển trường họ tên sang bảng tài khoản; cập nhật quy tắc xoay vòng refresh token.
- Chuyển lịch phụng vụ từ theo tuần sang theo ngày (thay `LiturgicalWeek` bằng `LiturgicalDay`), dọn các exception không còn dùng.
- Hoàn thành API thư viện bài hát: tìm kiếm, tạo, sửa, xoá, phân loại bài hát và yêu cầu kỹ năng cho bài.
- Hoàn thành API tài liệu âm nhạc: ca trưởng quản lý tài liệu; ca viên xem, tìm kiếm, lọc tài liệu và đánh dấu tiến độ học.
- Hoàn thành API tìm ca viên theo kỹ năng đã duyệt, xem hồ sơ ca viên kèm kỹ năng và vai trò.
- Bổ sung DTO cho các danh mục tra cứu (loại nghi thức, loại sự kiện, loại thánh lễ, chủ đề và phân loại bài hát).
- Viết test cho API danh mục, hồ sơ ca viên, quản lý tài khoản và khởi tạo dữ liệu.
- Viết tài liệu API (endpoint, quy ước xác thực) và hướng dẫn sửa endpoint cho nhóm.

**Số liệu** (chỉ tính repo backend, chỉ commit của Lưu Sa Trường)
- 20 commit, 174 file thay đổi, +13.808 / −454 dòng.
- Trong đó khoảng 7.339 dòng thêm là file migration do EF tự sinh.

**Tuần tới**


**Khó khăn / cần hỗ trợ**


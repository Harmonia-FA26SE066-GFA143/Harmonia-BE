# Changelog hợp đồng API

Những thay đổi Web và Mobile cần biết, mới nhất ở trên. Chi tiết từng endpoint ở `api.md`, mã lỗi ở `error-codes.md`.

## Quy ước

- **Thay đổi phá vỡ** (breaking): bỏ hoặc đổi tên endpoint / trường, đổi kiểu hay ý nghĩa của trường, thêm trường
  **bắt buộc** vào request, siết quyền (role cũ bị 403/404), đổi mã lỗi đang trả. Ghi rõ **[Phá vỡ]** và báo nhóm FE
  trước khi merge.
- **Không phá vỡ**: thêm endpoint, thêm trường vào response, thêm trường tuỳ chọn vào request, thêm mã lỗi mới cho tình
  huống trước đây chưa có. FE bỏ qua trường lạ, hiển thị câu chung cho mã lạ.
- Mỗi PR đổi hợp đồng thêm một dòng vào mục ngày tương ứng ở đây, cùng PR cập nhật `api.md` / `error-codes.md`.

## 2026-10-25

### [Phá vỡ] Nhiều ca đoàn — phạm vi ca trưởng (D8)

- Mỗi ca đoàn có **một** ca trưởng (`Choir.directorId`), do **Admin** gán. Chỉ Admin `POST` / `PUT api/choirs`.
- Ca trưởng chỉ thấy và quản lý ca đoàn của mình; ca đoàn của người khác trả **404** `CHOIR_NOT_FOUND`.
- Gán ca đoàn phục vụ và **ca đoàn phụ trách** (`isLead`) cho sự kiện: chỉ **ParishPriest**
  (`PUT api/liturgical-events/{id}/choirs`). Ca trưởng chỉ `GET`.
- Sự kiện nhiều ca đoàn: chỉ ca trưởng của ca đoàn phụ trách lập danh sách bài, mời tham gia, phân công. Ca đoàn phục vụ
  khác chỉ xem; sửa trả **403** `CHOIR_NOT_LEAD`. Có ca đoàn mà chưa chọn phụ trách: ca trưởng mọi ca đoàn phục vụ đều
  sửa được. Bảng đầy đủ ở `api.md` mục 4f.
- Bản ghi chưa gắn ca đoàn (sự kiện không có ca đoàn nào) thì ca trưởng nào cũng quản lý được như trước.

### Không phá vỡ

- Thông báo công bố / huỷ sự kiện: sự kiện đã gắn ca đoàn chỉ gửi cho ca viên và ca trưởng của các ca đoàn phục vụ;
  sự kiện chưa gắn ca đoàn vẫn gửi cả giáo xứ.
- `DELETE api/songs/{id}`: bài đã nằm trong danh sách bài hát, buổi tập hoặc bài luyện tập trả **409** `SONG_IN_USE`.
- `GET api/system-settings/public` gọi được **không cần đăng nhập** (chỉ trả `Parish.Name`, `Choir.Name`,
  `Contact.Email`). Thêm key `Contact.Email`. `SystemSettingDto` thêm `displayName`, `group`, `order`; danh sách sắp
  theo `order` thay vì `key`.
- `GET api/reports/columns/{type}`: cột và tiêu đề tiếng Việt của báo cáo, trùng với file xuất.
- `GET api/reports/activity-trend`: điểm danh và luyện tập theo tuần / tháng / năm cho Admin và ca trưởng.
- `GET api/songs`: tìm không phân biệt dấu (cả tìm ca viên, tìm tài liệu); thêm bộ lọc `isClassified`,
  `missingMaterialType`; `SongDto` thêm `materialCounts`, `isClassified`, `timesUsed`, `lastUsedDate`.
- `PUT api/music-materials/{id}/file`: thay file tài liệu, giữ tiến độ học (tuỳ chọn `resetProgress`).
- Bài luyện tập nhiều tài liệu: request thêm `materialIds`; `PracticeAssignmentDto` thêm `materialIds`,
  `PracticeAssignmentDetailDto` thêm `materials`. `materialId` vẫn còn (= tài liệu đầu tiên).
- `LiturgicalDayDto` thêm `celebrationNameVi`, `rankVi`, `seasonNameVi`; bản dịch do Admin quản lý ở
  `api/liturgical-name-translations`.

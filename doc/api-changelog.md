# Changelog hợp đồng API

Những thay đổi Web và Mobile cần biết, mới nhất ở trên. Chi tiết từng endpoint ở `api.md`, mã lỗi ở `error-codes.md`.

## Quy ước

- **Thay đổi phá vỡ** (breaking): bỏ hoặc đổi tên endpoint / trường, đổi kiểu hay ý nghĩa của trường, thêm trường
  **bắt buộc** vào request, siết quyền (role cũ bị 403/404), đổi mã lỗi đang trả. Ghi rõ **[Phá vỡ]** và báo nhóm FE
  trước khi merge.
- **Không phá vỡ**: thêm endpoint, thêm trường vào response, thêm trường tuỳ chọn vào request, thêm mã lỗi mới cho tình
  huống trước đây chưa có. FE bỏ qua trường lạ, hiển thị câu chung cho mã lạ.
- Mỗi PR đổi hợp đồng thêm một dòng vào mục ngày tương ứng ở đây, cùng PR cập nhật `api.md` / `error-codes.md`.

## 2026-10-26

Phân loại bài hát chi tiết hơn (theo dieuca.net) và phân công 3 tầng: đồng ca không phân công, vai **cả buổi lễ**
(organ, bắt nhịp), vai **theo bài** (xướng đáp ca, solo). Cần migration trước khi deploy.

### [Phá vỡ] Phân công phục vụ

- `RosterShortageDto.songListItemId` và `songTitle` **có thể `null`**: dòng thiếu người cho cả buổi lễ. Web đang hiển thị
  `"{songTitle}: thiếu …"` phải đổi cho dòng này (ví dụ "Cả buổi lễ: thiếu 1 Organ").
- `RosterShortageDto.requiredCount` của dòng **theo bài** giờ là **phần thêm** ngoài người cả buổi lễ: bài cần Guitar ×2, cả
  lễ có Guitar ×1 → dòng thiếu của bài có `requiredCount = 1`. Hiển thị "cần **thêm** 1 guitar". Bài cần ≤ số của cả lễ không
  còn sinh dòng thiếu nào.
- `RosterAssignmentDto.songListItemId = null` = dòng cả buổi lễ. `MyAssignmentDto` cũng vậy.
- `GET api/liturgical-events/{id}/preparation-status`: `rosterShortages` **khác `null`** khi sự kiện có yêu cầu cả buổi lễ,
  kể cả khi chưa có danh sách bài được duyệt.
- `POST api/service-rosters/suggestions`, `GET …/shortages`: không còn trả `ROSTER_SONG_LIST_NOT_APPROVED` khi sự kiện có yêu
  cầu cả buổi lễ — chạy trên phần cả buổi lễ.
- `ROSTER_ASSIGNMENT_DUPLICATE` thêm trường hợp: một người giữ kỹ năng cho cả buổi lễ không được gán thêm cùng kỹ năng ở một
  bài, và ngược lại (thêm và thay người).
- `PUT api/song-list-items/{id}/personnel-requirements`: bỏ một kỹ năng đang có người được gán → 409
  `PERSONNEL_REQUIREMENT_IN_USE` (trước đây gỡ được, để lại dòng phân công mồ côi).

### [Phá vỡ] Chủ đề bài hát

- `POST` / `PUT api/lookups/song-themes` nhận thêm `parentId?`; `GET` trả thêm `parentId`. Chủ đề thành cây nhiều cấp.
- Tắt chủ đề còn con đang hoạt động → 409 `SONG_THEME_HAS_ACTIVE_CHILDREN` (mã mới). Bật / tạo chủ đề dưới cha đã tắt → 400
  `SONG_THEME_PARENT_INVALID` (mã mới).
- `GET api/songs?songThemeId=` giờ trả cả bài của các chủ đề con cháu.

### Không phá vỡ

- Endpoint mới:
  - `GET` / `PUT api/liturgical-events/{id}/personnel-requirements` — vai cả buổi lễ.
  - `POST api/service-rosters/{id}/whole-service-finalization` — chốt sớm vai cả buổi lễ; sau đó dòng cả buổi lễ chỉ còn
    **thay người** được, `notifications` gửi được ngay cho các dòng đó. `ServiceRosterDto` thêm `wholeServiceFinalizedAt`.
  - `PUT api/service-rosters/assignments/{id}/note` — ghi chú vai. `RosterAssignmentDto`, `MyAssignmentDto` thêm `note`;
    thêm / thay người nhận `note?`.
  - `GET api/liturgical-events/{id}/song-suggestions?slotId=` — gợi ý bài cho sự kiện.
  - `GET` / `PUT api/lookups/liturgical-slots/{id}/requirements` — người vị trí luôn cần (Admin ghi).
- `POST api/service-rosters/assignments`: `songListItemId` thành tuỳ chọn (bỏ trống = dòng cả buổi lễ).
- Phân loại bài: `liturgicalSlotIds` / `liturgicalSlots` (vị trí trong lễ); `vocalRequirements` / `instrumentRequirements` thêm
  `requiredCount` (mặc định 1). `GET api/songs` thêm lọc `liturgicalSlotId`.
- `POST api/liturgical-events`: bỏ trống `liturgicalSeasonId` → server điền mùa đang hoạt động **chứa ngày lễ**. Lễ Vọng tối
  24/12 vì vậy được điền Mùa Vọng; cha xứ tự chọn Giáng Sinh nếu muốn (chủ ý, không tự đoán theo giờ).
- Yêu cầu nhân sự của bài được **điền sẵn** khi tạo / sửa danh sách: giữ yêu cầu của cùng bài cùng vị trí ở bản trước; bài mới
  lấy từ thư viện (bỏ bè S/A/T/B) + mặc định của vị trí. Sửa danh sách không còn làm mất yêu cầu đã khai.
- Dữ liệu mẫu (loại lễ, nghi thức, vị trí trong lễ, cây chủ đề, mặc định Đáp ca / Tung hô Tin Mừng) chỉ nạp vào **bảng đang
  trống**. Trên Azure các bảng đã có dữ liệu thì Admin tự cấu hình; mặc định Đáp ca chỉ nạp khi đã có kỹ năng nhóm Đáp ca.

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

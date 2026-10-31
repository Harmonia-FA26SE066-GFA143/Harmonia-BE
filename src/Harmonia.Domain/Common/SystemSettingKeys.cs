using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Common;

/// <summary>
/// Every setting the system reads (UC-33 / FE-51). The Admin edits values, never the set of keys: a key no
/// code reads would change nothing. DataSeeder adds the rows missing from the database at startup.
/// </summary>
public static class SystemSettingKeys
{
    public const string ParishName = "Parish.Name";

    public const string ChoirName = "Choir.Name";

    public const string ContactEmail = "Contact.Email";

    public const string SystemAnnouncement = "System.Announcement";

    public const string RosterRecentServiceDays = "Roster.RecentServiceDays";

    public const string RosterAiSuggestionEnabled = "Roster.AiSuggestionEnabled";

    public const string RosterEventOverlapMinutes = "Roster.EventOverlapMinutes";

    public const string PracticeAllowLateSubmission = "Practice.AllowLateSubmission";

    public const string RehearsalAttendanceOpensMinutesBefore = "Rehearsal.AttendanceOpensMinutesBefore";

    private const string GeneralGroup = "Thông tin chung";

    // In the order the settings page shows them; the index is SystemSettingDto.Order.
    public static readonly IReadOnlyList<SystemSettingDefinition> All =
    [
        new(ParishName, SettingDataType.String, string.Empty, "Tên giáo xứ", GeneralGroup,
            "Tên giáo xứ, hiển thị trên ứng dụng.", IsPublic: true, IsAnonymous: true),
        new(ChoirName, SettingDataType.String, string.Empty, "Tên ca đoàn", GeneralGroup,
            "Tên ca đoàn, hiển thị trên ứng dụng.", IsPublic: true, IsAnonymous: true),
        new(ContactEmail, SettingDataType.String, string.Empty, "Email liên hệ", GeneralGroup,
            "Email liên hệ hiển thị ở trang chủ và trang đăng nhập.", IsPublic: true, IsAnonymous: true),
        new(SystemAnnouncement, SettingDataType.String, string.Empty, "Thông báo hệ thống", GeneralGroup,
            "Thông báo hiển thị cho mọi người dùng; để trống thì không hiển thị.", IsPublic: true),
        new(RosterRecentServiceDays, SettingDataType.Int, "60", "Số ngày tính là phục vụ gần đây", "Phân công",
            "Số ngày gần nhất được tính là đã phục vụ gần đây khi đề xuất phân công.", MinValue: 1, MaxValue: 365),
        new(RosterAiSuggestionEnabled, SettingDataType.Bool, "true", "Đề xuất phân công bằng AI", "Phân công",
            "Có dùng AI để đề xuất phân công không; tắt thì chỉ dùng quy tắc có sẵn."),
        new(RosterEventOverlapMinutes, SettingDataType.Int, "120", "Khoảng cách tối thiểu giữa hai lễ (phút)", "Phân công",
            "Không phân công một ca viên vào hai lễ cùng ngày bắt đầu cách nhau ít hơn số phút này; 0 thì chỉ chặn hai lễ cùng giờ.",
            MinValue: 0, MaxValue: 720),
        new(PracticeAllowLateSubmission, SettingDataType.Bool, "false", "Cho nộp bản thu trễ hạn", "Luyện tập",
            "Cho phép ca viên nộp bản thu sau hạn của bài tập."),
        new(RehearsalAttendanceOpensMinutesBefore, SettingDataType.Int, "0", "Mở điểm danh trước giờ tập (phút)",
            "Buổi tập", "Số phút trước giờ bắt đầu buổi tập được phép điểm danh.", MinValue: 0, MaxValue: 120),
    ];

    public static SystemSettingDefinition? Find(string key) => All.FirstOrDefault(x => x.Key == key);
}

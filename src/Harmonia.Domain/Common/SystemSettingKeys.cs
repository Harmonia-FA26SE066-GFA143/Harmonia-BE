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

    public const string SystemAnnouncement = "System.Announcement";

    public const string RosterRecentServiceDays = "Roster.RecentServiceDays";

    public const string RosterAiSuggestionEnabled = "Roster.AiSuggestionEnabled";

    public const string PracticeAllowLateSubmission = "Practice.AllowLateSubmission";

    public const string RehearsalAttendanceOpensMinutesBefore = "Rehearsal.AttendanceOpensMinutesBefore";

    public static readonly IReadOnlyList<SystemSettingDefinition> All =
    [
        new(ParishName, SettingDataType.String, string.Empty, "Tên giáo xứ, hiển thị trên ứng dụng.", IsPublic: true),
        new(ChoirName, SettingDataType.String, string.Empty, "Tên ca đoàn, hiển thị trên ứng dụng.", IsPublic: true),
        new(SystemAnnouncement, SettingDataType.String, string.Empty,
            "Thông báo hiển thị cho mọi người dùng; để trống thì không hiển thị.", IsPublic: true),
        new(RosterRecentServiceDays, SettingDataType.Int, "60",
            "Số ngày gần nhất được tính là đã phục vụ gần đây khi đề xuất phân công.", MinValue: 1, MaxValue: 365),
        new(RosterAiSuggestionEnabled, SettingDataType.Bool, "true",
            "Có dùng AI để đề xuất phân công không; tắt thì chỉ dùng quy tắc có sẵn."),
        new(PracticeAllowLateSubmission, SettingDataType.Bool, "false",
            "Cho phép ca viên nộp bản thu sau hạn của bài tập."),
        new(RehearsalAttendanceOpensMinutesBefore, SettingDataType.Int, "0",
            "Số phút trước giờ bắt đầu buổi tập được phép điểm danh.", MinValue: 0, MaxValue: 120),
    ];

    public static SystemSettingDefinition? Find(string key) => All.FirstOrDefault(x => x.Key == key);
}

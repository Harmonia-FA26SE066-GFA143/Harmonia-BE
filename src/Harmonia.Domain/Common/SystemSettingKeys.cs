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
        new(ParishName, SettingDataType.String, string.Empty, "Name of the parish, shown by the clients.", IsPublic: true),
        new(ChoirName, SettingDataType.String, string.Empty, "Name of the choir, shown by the clients.", IsPublic: true),
        new(SystemAnnouncement, SettingDataType.String, string.Empty,
            "Notice shown to every user; empty shows nothing.", IsPublic: true),
        new(RosterRecentServiceDays, SettingDataType.Int, "60",
            "How many days back a service counts as recent when suggesting a roster.", MinValue: 1, MaxValue: 365),
        new(RosterAiSuggestionEnabled, SettingDataType.Bool, "true",
            "Whether roster suggestions ask the AI; when off they are filled by the built-in rules only."),
        new(PracticeAllowLateSubmission, SettingDataType.Bool, "false",
            "Whether a member may still submit a recording after the assignment's due date."),
        new(RehearsalAttendanceOpensMinutesBefore, SettingDataType.Int, "0",
            "How many minutes before a rehearsal starts attendance may be recorded.", MinValue: 0, MaxValue: 120),
    ];

    public static SystemSettingDefinition? Find(string key) => All.FirstOrDefault(x => x.Key == key);
}

using System.Text.RegularExpressions;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

/// <summary>
/// Vietnamese wording of the calendar names built from a pattern — weekdays and numbered Sundays such as
/// "Tuesday of the 2nd Week of Advent" or "25th Sunday of Ordinary Time" — which no translation row lists one by one.
/// Vietnamese text is allowed here on purpose (02-naming.md): it is grammar, not data the Admin edits.
/// Ported from the Web's celebrationNames.ts.
/// </summary>
public static partial class LiturgicalNamePatterns
{
    private static readonly Dictionary<string, string> Days = new()
    {
        ["Monday"] = "Thứ Hai",
        ["Tuesday"] = "Thứ Ba",
        ["Wednesday"] = "Thứ Tư",
        ["Thursday"] = "Thứ Năm",
        ["Friday"] = "Thứ Sáu",
        ["Saturday"] = "Thứ Bảy",
    };

    private static readonly Dictionary<string, int> Ordinals = new()
    {
        ["First"] = 1, ["Second"] = 2, ["Third"] = 3, ["Fourth"] = 4, ["Fifth"] = 5, ["Sixth"] = 6, ["Seventh"] = 7,
    };

    private static readonly Dictionary<string, string> Seasons = new()
    {
        ["Advent"] = "Mùa Vọng",
        ["Lent"] = "Mùa Chay",
        ["Easter"] = "Phục Sinh",
        ["Ordinary Time"] = "Thường niên",
    };

    private const string DayName = "(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday)";

    // Names come from an external calendar API, so a match must not run unbounded (Sonar S6444).
    private static Regex Rx(string pattern) => new(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static readonly (Regex Pattern, Func<GroupCollection, string> Translate)[] Rules =
    [
        (Rx($@"^{DayName} of the (\d+)(?:st|nd|rd|th) Week of (Advent|Lent|Easter|Ordinary Time)$"),
            g => $"{Days[g[1].Value]} tuần {Roman(int.Parse(g[2].Value))} {Seasons[g[3].Value]}"),
        (Rx(@"^(\d+)(?:st|nd|rd|th) Sunday of Ordinary Time$"),
            g => $"Chúa nhật {Roman(int.Parse(g[1].Value))} Thường niên"),
        (Rx("^(First|Second|Third|Fourth|Fifth|Sixth|Seventh) Sunday of (Advent|Lent|Easter)$"),
            g => $"Chúa nhật {Roman(Ordinals[g[1].Value])} {Seasons[g[2].Value]}"),
        (Rx($"^{DayName} - Christmas Weekday$"), g => $"{Days[g[1].Value]} Mùa Giáng Sinh"),
        (Rx($"^{DayName} after Ash Wednesday$"), g => $"{Days[g[1].Value]} sau Lễ Tro"),
        (Rx($"^{DayName} of Holy Week$"), g => $"{Days[g[1].Value]} Tuần Thánh"),
        (Rx($"^{DayName} of the Octave of Easter$"), g => $"{Days[g[1].Value]} trong Tuần Bát Nhật Phục Sinh"),
        (Rx(@"^(\d+)(?:st|nd|rd|th) Day of the Octave of Christmas$"),
            g => $"Ngày thứ {g[1].Value} trong Tuần Bát Nhật Giáng Sinh"),
    ];

    /// <summary>The Vietnamese name when <paramref name="englishName"/> follows a known pattern; null otherwise.</summary>
    public static string? Translate(string englishName)
    {
        foreach (var (pattern, translate) in Rules)
        {
            var match = pattern.Match(englishName);
            if (match.Success) return translate(match.Groups);
        }

        return null;
    }

    /// <summary>
    /// The Admin's row first, then (celebrations only) the patterns, else the English name unchanged, so a saint new
    /// to the calendar still shows.
    /// </summary>
    public static string Translate(
        LiturgicalNameKind kind, string englishName,
        IReadOnlyDictionary<(LiturgicalNameKind Kind, string EnglishName), string> table) =>
        table.GetValueOrDefault((kind, englishName))
        ?? (kind == LiturgicalNameKind.Celebration ? Translate(englishName) : null)
        ?? englishName;

    /// <summary>1–39 as Roman numerals, as Vietnamese liturgical calendars number the weeks.</summary>
    private static string Roman(int n) =>
        new string('X', n / 10) + new[] { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" }[n % 10];
}

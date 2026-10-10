using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class CatholicCalendarParser : ICatholicCalendarParser
{
    private const int MaxCelebrationNameLength = 200;

    private static readonly string[] ColourEmojis = ["⚪", "🟢", "🟣", "🔴"];

    private static readonly Regex RankPattern = new(@"^\[(?<code>[A-Za-z*]+)\]\s*(?<name>.*)$", RegexOptions.Compiled);

    // RFC 5545 TEXT escapes: \n \N \, \; \\
    private static readonly Regex TextEscapePattern = new(@"\\([nN,;\\])", RegexOptions.Compiled);

    private static readonly (string Keyword, string Season)[] SeasonKeywords =
    [
        ("Mùa Vọng", "Advent"),
        ("Giáng Sinh", "Christmas"),
        ("Mùa Chay", "Lent"),
        ("Tuần Thánh", "Lent"),
        ("Phục Sinh", "Easter"),
        ("Mùa Quanh Năm", "OrdinaryTime"),
    ];

    public async Task<Result<List<CalendarDayResult>>> ParseAsync(Stream content, CancellationToken cancellationToken)
    {
        string raw;
        using (var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken);
        }

        if (!raw.TrimStart().StartsWith("BEGIN:VCALENDAR", StringComparison.Ordinal))
        {
            return Result<List<CalendarDayResult>>.Failure(ErrorCodes.CalendarFileInvalid);
        }

        var byDate = new Dictionary<DateOnly, (CalendarDayResult Result, int Priority)>();

        DateOnly? currentDate = null;
        string? currentSummary = null;

        foreach (var line in UnfoldLines(raw))
        {
            if (line.StartsWith("BEGIN:VEVENT", StringComparison.Ordinal))
            {
                currentDate = null;
                currentSummary = null;
            }
            else if (line.StartsWith("END:VEVENT", StringComparison.Ordinal))
            {
                if (currentDate is { } date && currentSummary is not null)
                {
                    var (celebrationName, rank, priority) = ParseSummary(currentSummary);
                    var seasonName = DetectSeason(celebrationName);

                    if (!byDate.TryGetValue(date, out var existing) || priority > existing.Priority)
                    {
                        byDate[date] = (new CalendarDayResult(date, celebrationName, rank, seasonName), priority);
                    }
                }
            }
            else
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                // Property parameters sit before the colon, e.g. "DTSTART;VALUE=DATE:20261225".
                var propertyName = line[..separator].Split(';', 2)[0];
                var value = line[(separator + 1)..];

                if (propertyName == "DTSTART")
                {
                    currentDate = ParseDate(value);
                }
                else if (propertyName == "SUMMARY")
                {
                    currentSummary = UnescapeText(value);
                }
            }
        }

        if (byDate.Count == 0)
        {
            return Result<List<CalendarDayResult>>.Failure(ErrorCodes.CalendarFileInvalid);
        }

        return Result<List<CalendarDayResult>>.Success(
            byDate.Values.Select(x => x.Result).OrderBy(x => x.Date).ToList());
    }

    /// <summary>Un-folds RFC 5545 continuation lines (a line starting with one space/tab belongs to the previous line).</summary>
    private static List<string> UnfoldLines(string raw)
    {
        var physicalLines = raw.Replace("\r\n", "\n").Split('\n');
        var logical = new List<string>();

        foreach (var line in physicalLines)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && logical.Count > 0)
            {
                logical[^1] += line[1..];
            }
            else if (line.Length > 0)
            {
                logical.Add(line);
            }
        }

        return logical;
    }

    /// <summary>Reads the leading yyyyMMdd of a DATE or DATE-TIME value; null when it is not a date.</summary>
    private static DateOnly? ParseDate(string value)
    {
        var trimmed = value.Trim();

        return trimmed.Length >= 8
            && DateOnly.TryParseExact(trimmed[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : null;
    }

    private static string UnescapeText(string value) =>
        TextEscapePattern.Replace(value, m => m.Groups[1].Value is "n" or "N" ? " " : m.Groups[1].Value);

    private static (string CelebrationName, string? Rank, int Priority) ParseSummary(string summary)
    {
        var rest = summary;

        foreach (var colour in ColourEmojis)
        {
            if (rest.StartsWith(colour, StringComparison.Ordinal))
            {
                rest = rest[colour.Length..].TrimStart();
                break;
            }
        }

        var match = RankPattern.Match(rest);
        if (match.Success)
        {
            var code = match.Groups["code"].Value;
            var name = Truncate(match.Groups["name"].Value);

            return code switch
            {
                "T" => (name, "Solemnity", 5),
                "K" => (name, "Feast", 4),
                "N" => (name, "Obligatory Memorial", 3),
                "n" or "n*" => (name, "Optional Memorial", 1),
                _ => (name, null, 2),
            };
        }

        return (Truncate(rest), null, 2);
    }

    private static string Truncate(string name) =>
        name.Length <= MaxCelebrationNameLength ? name : name[..MaxCelebrationNameLength];

    private static string? DetectSeason(string celebrationName)
    {
        foreach (var (keyword, season) in SeasonKeywords)
        {
            if (celebrationName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return season;
            }
        }

        return null;
    }
}
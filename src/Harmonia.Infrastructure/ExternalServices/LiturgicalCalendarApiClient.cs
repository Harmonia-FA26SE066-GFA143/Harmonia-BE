using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Calls the Liturgical Calendar API (litcal.johnromanodorazio.com) for the General Roman Calendar. Base address and
/// timeout are set at registration. The API has no Vietnamese locale, so names come in English.
/// </summary>
public class LiturgicalCalendarApiClient(
    HttpClient httpClient, ILogger<LiturgicalCalendarApiClient> logger) : ILiturgicalCalendarClient
{
    private const int MaxCelebrationNameLength = 200;

    // Vietnam keeps Epiphany, Ascension and Corpus Christi on the following Sunday.
    private const string Query =
        "locale=en&year_type=CIVIL&epiphany=SUNDAY_JAN2_JAN8&ascension=SUNDAY&corpus_christi=SUNDAY";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<Result<List<CalendarDayResult>>> GetYearAsync(int year, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/v5/calendar/{year}?{Query}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Liturgical calendar request failed with status {StatusCode}", (int)response.StatusCode);
                return Result<List<CalendarDayResult>>.Failure(ErrorCodes.ExternalCalendarFailed);
            }

            var body = await response.Content.ReadFromJsonAsync<CalendarResponse>(Json, cancellationToken);
            var days = (body?.Litcal ?? [])
                .Where(x => x.IsVigilMass != true && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => (Date: DateOnly.FromDateTime(x.Date.Date), Event: x))
                .Where(x => x.Date.Year == year)
                // Several celebrations can share a date; the highest grade is the day's celebration.
                .GroupBy(x => x.Date)
                .Select(g => ToDay(g.Key, g.MaxBy(x => x.Event.Grade).Event))
                .OrderBy(x => x.Date)
                .ToList();

            if (days.Count == 0)
            {
                logger.LogWarning("Liturgical calendar returned no days for {Year}", year);
                return Result<List<CalendarDayResult>>.Failure(ErrorCodes.ExternalCalendarFailed);
            }

            return Result<List<CalendarDayResult>>.Success(days);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or NotSupportedException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Timeout or an unexpected response shape.
            logger.LogWarning(ex, "Liturgical calendar request failed for {Year}", year);
            return Result<List<CalendarDayResult>>.Failure(ErrorCodes.ExternalCalendarFailed);
        }
    }

    private static CalendarDayResult ToDay(DateOnly date, CalendarEvent calendarEvent)
    {
        var name = calendarEvent.Name!.Trim();

        return new CalendarDayResult(
            date,
            name.Length <= MaxCelebrationNameLength ? name : name[..MaxCelebrationNameLength],
            ToRank(calendarEvent.Grade),
            ToSeason(calendarEvent.LiturgicalSeason));
    }

    // Grades: 7 precedence over solemnities, 6 solemnity, 5 feast of the Lord, 4 feast, 3 memorial,
    // 2 optional memorial, 1 commemoration, 0 weekday.
    private static string? ToRank(int grade) => grade switch
    {
        >= 6 => "Solemnity",
        4 or 5 => "Feast",
        3 => "Obligatory Memorial",
        1 or 2 => "Optional Memorial",
        _ => null,
    };

    // The Triduum counts as Lent, as the season lookup has no entry of its own for it.
    private static string? ToSeason(string? season) => season switch
    {
        "ADVENT" => "Advent",
        "CHRISTMAS" => "Christmas",
        "LENT" or "EASTER_TRIDUUM" => "Lent",
        "EASTER" => "Easter",
        "ORDINARY_TIME" => "OrdinaryTime",
        _ => null,
    };

    private sealed record CalendarResponse(List<CalendarEvent>? Litcal);

    private sealed record CalendarEvent(
        string? Name, DateTimeOffset Date, int Grade, string? LiturgicalSeason, bool? IsVigilMass);
}

using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Reads the Catholic liturgical calendar from an external source.</summary>
public interface ILiturgicalCalendarClient
{
    /// <summary>
    /// One entry per date of the civil <paramref name="year"/>, holding the highest-ranked celebration of that date.
    /// Fails with <c>EXTERNAL_CALENDAR_FAILED</c> when the source is unreachable or answers unexpectedly.
    /// </summary>
    Task<Result<List<CalendarDayResult>>> GetYearAsync(int year, CancellationToken cancellationToken);
}

/// <summary>
/// <paramref name="Rank"/> is Solemnity, Feast, Obligatory Memorial, Optional Memorial or null for a weekday;
/// <paramref name="SeasonName"/> is Advent, Christmas, Lent, Easter or OrdinaryTime.
/// </summary>
public record CalendarDayResult(DateOnly Date, string CelebrationName, string? Rank, string? SeasonName);

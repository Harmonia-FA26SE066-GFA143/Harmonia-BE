using Harmonia.Application.Common.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ICatholicCalendarParser
{
    
    Task<Result<List<CalendarDayResult>>> ParseAsync(Stream content, CancellationToken cancellationToken);
}

public record CalendarDayResult(DateOnly Date, string CelebrationName, string? Rank, string? SeasonName);
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ILiturgicalDayService
{
    Task<Result<LiturgicalDayDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Cached days of the range for a calendar view (B22); days not synced yet are simply absent.</summary>
    Task<Result<List<LiturgicalDayDto>>> GetRangeAsync(
        SearchLiturgicalDaysRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the year from the liturgical calendar and replaces the cached days of that year.
    /// Returns the number of days stored.
    /// </summary>
    Task<Result<int>> SyncAsync(SyncLiturgicalDaysRequest request, CancellationToken cancellationToken);
}

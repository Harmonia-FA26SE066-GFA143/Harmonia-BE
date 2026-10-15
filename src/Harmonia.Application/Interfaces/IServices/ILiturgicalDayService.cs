using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ILiturgicalDayService
{
    Task<Result<LiturgicalDayDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Cached days of the range for a calendar view (B22); days not imported yet are simply absent.</summary>
    Task<Result<List<LiturgicalDayDto>>> GetRangeAsync(
        SearchLiturgicalDaysRequest request, CancellationToken cancellationToken);

    Task<Result<int>> ImportAsync(Stream content, string fileName, long length, CancellationToken cancellationToken);
}
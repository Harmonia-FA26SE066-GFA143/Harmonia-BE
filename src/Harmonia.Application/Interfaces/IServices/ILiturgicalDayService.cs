using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ILiturgicalDayService
{
    Task<Result<LiturgicalDayDto>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task<Result<int>> ImportAsync(Stream content, string fileName, long length, CancellationToken cancellationToken);
}
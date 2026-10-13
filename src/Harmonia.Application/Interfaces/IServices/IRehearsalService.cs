using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IRehearsalService
{
    Task<Result<RehearsalDto>> CreateAsync(CreateRehearsalRequest request, CancellationToken cancellationToken);
}
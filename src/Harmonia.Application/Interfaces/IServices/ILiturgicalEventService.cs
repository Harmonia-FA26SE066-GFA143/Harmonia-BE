using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ILiturgicalEventService
{
    Task<Result<LiturgicalEventDto>> CreateAsync(
        CreateLiturgicalEventRequest request, CancellationToken cancellationToken);

    Task<Result<LiturgicalEventDto>> PublishAsync(Guid id, CancellationToken cancellationToken);
}
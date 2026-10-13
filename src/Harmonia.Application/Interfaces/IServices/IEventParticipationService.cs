using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IEventParticipationService
{
    
    Task<Result<List<EventParticipationDto>>> SendRequestsAsync(SendParticipationRequestsRequest request, CancellationToken cancellationToken);

    Task<Result<EventParticipationSummaryDto>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Result<EventParticipationDto>> RespondAsync(Guid userId, Guid eventId, RespondParticipationRequest request, CancellationToken cancellationToken);
}
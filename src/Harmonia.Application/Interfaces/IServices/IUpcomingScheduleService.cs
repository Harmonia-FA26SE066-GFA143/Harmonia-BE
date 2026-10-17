using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IUpcomingScheduleService
{
    Task<Result<List<LiturgicalEventSummaryDto>>> GetUpcomingEventsAsync(CancellationToken cancellationToken);

    Task<Result<List<RehearsalSummaryDto>>> GetUpcomingRehearsalsAsync(CancellationToken cancellationToken);

    /// <summary>Upcoming events where the calling member serves, once the roster is finalized and they are notified.</summary>
    Task<Result<List<MyAssignedEventDto>>> GetMyAssignmentsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A Published event with the caller's own request, assignments and attendance; anything else is not found.</summary>
    Task<Result<MemberEventDetailDto>> GetEventDetailAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);

    /// <summary>A standalone rehearsal or one of a Published event, with the caller's own attendance.</summary>
    Task<Result<MemberRehearsalDetailDto>> GetRehearsalDetailAsync(
        Guid userId, Guid rehearsalId, CancellationToken cancellationToken);
}
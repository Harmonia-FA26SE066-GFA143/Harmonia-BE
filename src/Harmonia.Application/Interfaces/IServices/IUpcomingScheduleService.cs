using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IUpcomingScheduleService
{
    Task<Result<List<LiturgicalEventSummaryDto>>> GetUpcomingEventsAsync(CancellationToken cancellationToken);

    Task<Result<List<RehearsalSummaryDto>>> GetUpcomingRehearsalsAsync(CancellationToken cancellationToken);
}
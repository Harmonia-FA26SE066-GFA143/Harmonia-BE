using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class UpcomingScheduleService(
    ILiturgicalEventRepository liturgicalEventRepository,
    IRehearsalRepository rehearsalRepository,
    IMapper mapper) : IUpcomingScheduleService
{
    public async Task<Result<List<LiturgicalEventSummaryDto>>> GetUpcomingEventsAsync(
        CancellationToken cancellationToken)
    {
        var events = await liturgicalEventRepository.GetUpcomingPublishedAsync(
            VietnamTime.Today, cancellationToken);

        return Result<List<LiturgicalEventSummaryDto>>.Success(
            mapper.Map<List<LiturgicalEventSummaryDto>>(events));
    }

    public async Task<Result<List<RehearsalSummaryDto>>> GetUpcomingRehearsalsAsync(
        CancellationToken cancellationToken)
    {
        var rehearsals = await rehearsalRepository.GetUpcomingAsync(DateTime.UtcNow, cancellationToken);

        return Result<List<RehearsalSummaryDto>>.Success(mapper.Map<List<RehearsalSummaryDto>>(rehearsals));
    }
}
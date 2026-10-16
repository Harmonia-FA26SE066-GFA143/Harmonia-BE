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
    IMemberProfileRepository memberProfileRepository,
    IMapper mapper) : IUpcomingScheduleService
{
    public async Task<Result<List<MyAssignedEventDto>>> GetMyAssignmentsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<List<MyAssignedEventDto>>.Failure(ErrorCodes.MemberNotFound);
        }

        var events = await liturgicalEventRepository.GetUpcomingAssignedToMemberAsync(
            profile.Id, VietnamTime.Today, cancellationToken);

        return Result<List<MyAssignedEventDto>>.Success(mapper.Map<List<MyAssignedEventDto>>(events));
    }

    public async Task<Result<MemberEventDetailDto>> GetEventDetailAsync(
        Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(userId, cancellationToken);
        var liturgicalEvent = await liturgicalEventRepository.GetPublishedForMemberAsync(
            eventId, memberId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<MemberEventDetailDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        var dto = mapper.Map<MemberEventDetailDto>(liturgicalEvent);
        if (liturgicalEvent.EventParticipations.FirstOrDefault() is { } participation)
        {
            dto.Participation = EventParticipationService.ToMyParticipationDto(participation, liturgicalEvent, mapper);
        }

        return Result<MemberEventDetailDto>.Success(dto);
    }

    public async Task<Result<MemberRehearsalDetailDto>> GetRehearsalDetailAsync(
        Guid userId, Guid rehearsalId, CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(userId, cancellationToken);
        var rehearsal = await rehearsalRepository.GetVisibleForMemberAsync(rehearsalId, memberId, cancellationToken);

        return rehearsal is null
            ? Result<MemberRehearsalDetailDto>.Failure(ErrorCodes.RehearsalNotFound)
            : Result<MemberRehearsalDetailDto>.Success(mapper.Map<MemberRehearsalDetailDto>(rehearsal));
    }

    // Other roles have no profile; Guid.Empty matches no row, so their own-row sections come back empty.
    private async Task<Guid> GetMemberIdAsync(Guid userId, CancellationToken cancellationToken) =>
        (await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken))?.Id ?? Guid.Empty;

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
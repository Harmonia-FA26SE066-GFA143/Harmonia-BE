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
    IChoirScopeService choirScope,
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
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        var liturgicalEvent = await liturgicalEventRepository.GetPublishedForMemberAsync(
            eventId, profile?.Id ?? Guid.Empty, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<MemberEventDetailDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        var dto = mapper.Map<MemberEventDetailDto>(liturgicalEvent);
        // A participation row comes back only for the caller's own profile, so profile is set here.
        if (liturgicalEvent.EventParticipations.FirstOrDefault() is { } participation)
        {
            dto.Participation = EventParticipationService.ToMyParticipationDto(
                participation, liturgicalEvent, profile!.Status, mapper);
        }

        return Result<MemberEventDetailDto>.Success(dto);
    }

    public async Task<Result<MemberRehearsalDetailDto>> GetRehearsalDetailAsync(
        Guid userId, Guid rehearsalId, CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(userId, cancellationToken);
        var rehearsal = await rehearsalRepository.GetVisibleForMemberAsync(
            rehearsalId, memberId, await GetVisibleChoirIdsAsync(userId, cancellationToken), cancellationToken);

        return rehearsal is null
            ? Result<MemberRehearsalDetailDto>.Failure(ErrorCodes.RehearsalNotFound)
            : Result<MemberRehearsalDetailDto>.Success(mapper.Map<MemberRehearsalDetailDto>(rehearsal));
    }

    /// <summary>
    /// A choir member sees the rehearsals of their own choirs (and those with no choir); a director those of the
    /// choirs they lead; other roles every rehearsal.
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> GetVisibleChoirIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await choirScope.GetDirectedChoirIdsAsync(cancellationToken) is { } directed)
        {
            return directed;
        }

        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        return profile is null ? null : await choirScope.GetMemberChoirIdsAsync(profile.Id, cancellationToken);
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
        Guid userId, GetScheduleRehearsalsRequest request, CancellationToken cancellationToken)
    {
        var choirIds = await GetVisibleChoirIdsAsync(userId, cancellationToken);
        var rehearsals = request is { FromDate: { } fromDate, ToDate: { } toDate }
            ? await rehearsalRepository.GetStartingBetweenAsync(fromDate, toDate, choirIds, cancellationToken)
            : await rehearsalRepository.GetUpcomingAsync(DateTime.UtcNow, choirIds, cancellationToken);

        return Result<List<RehearsalSummaryDto>>.Success(mapper.Map<List<RehearsalSummaryDto>>(rehearsals));
    }
}
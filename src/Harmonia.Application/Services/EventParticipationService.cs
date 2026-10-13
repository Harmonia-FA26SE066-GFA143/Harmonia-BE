using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class EventParticipationService(
    IEventParticipationRepository participationRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IMemberProfileRepository memberProfileRepository,
    INotificationService notificationService,
    IMapper mapper) : IEventParticipationService
{
    public async Task<Result<List<EventParticipationDto>>> SendRequestsAsync(
        SendParticipationRequestsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.EventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.EventCancelled);
        }

        if (liturgicalEvent.Status != EventStatus.Published)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.EventNotPublished);
        }

        if (HasPassed(liturgicalEvent))
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.EventAlreadyPassed);
        }

        var invitees = await ResolveInviteesAsync(liturgicalEvent.Id, request.MemberIds, cancellationToken);
        if (!invitees.IsSuccess)
        {
            return Result<List<EventParticipationDto>>.Failure(invitees.Code!);
        }

        var members = invitees.Value!;
        if (members.Count == 0)
        {
            return Result<List<EventParticipationDto>>.Success([]);
        }

        // Member is left unset on purpose: it came from a read-only query, and assigning a detached
        // entity would make EF try to insert it. Names are filled into the DTOs below.
        var participations = members
            .Select(member => new EventParticipation
            {
                Id = Guid.NewGuid(),
                EventId = liturgicalEvent.Id,
                MemberId = member.Id,
                Status = ParticipationStatus.Invited,
                RequestedAt = DateTime.UtcNow,   // <-- UtcNow
            })
            .ToList();

        foreach (var participation in participations)
        {
            await participationRepository.AddAsync(participation, cancellationToken);
        }

        await participationRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.ParticipationRequest,
                Title: "Participation requested",
                Content: string.Create(
                    CultureInfo.InvariantCulture,
                    $"Please confirm your participation in the event on {liturgicalEvent.EventDate:yyyy-MM-dd} at {liturgicalEvent.Time:HH:mm}."),
                RecipientUserIds: members.Select(member => member.UserId).ToList(),
                ReferenceType: nameof(LiturgicalEvent),
                ReferenceId: liturgicalEvent.Id),
            cancellationToken);

        var dtos = mapper.Map<List<EventParticipationDto>>(participations);
        var names = members.ToDictionary(member => member.Id, member => member.User.FullName);
        foreach (var dto in dtos)
        {
            dto.MemberFullName = names[dto.MemberId];
        }

        return Result<List<EventParticipationDto>>.Success(dtos);
    }

    public async Task<Result<EventParticipationSummaryDto>> GetByEventAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<EventParticipationSummaryDto>.Failure(ErrorCodes.EventNotFound);
        }

        var participations = await participationRepository.GetByEventAsync(eventId, cancellationToken);
        var items = mapper.Map<List<EventParticipationDto>>(participations);

        return Result<EventParticipationSummaryDto>.Success(new EventParticipationSummaryDto
        {
            EventId = eventId,
            TotalCount = items.Count,
            InvitedCount = items.Count(x => x.Status == ParticipationStatus.Invited),
            ConfirmedCount = items.Count(x => x.Status == ParticipationStatus.Confirmed),
            DeclinedCount = items.Count(x => x.Status == ParticipationStatus.Declined),
            UnsureCount = items.Count(x => x.Status == ParticipationStatus.Unsure),
            Items = items,
        });
    }

    public async Task<Result<EventParticipationDto>> RespondAsync(
        Guid userId, Guid eventId, RespondParticipationRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.MemberNotFound);
        }

        if (profile.Status != MemberStatus.Active)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.MemberNotActive);
        }

        // Looked up by the caller's own member id, so another member's request is never reachable.
        var participation = await participationRepository.GetByEventAndMemberAsync(
            eventId, profile.Id, cancellationToken);
        if (participation is null)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.ParticipationNotInvited);
        }

        if (participation.LiturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.EventCancelled);
        }

        // A member answers once; the answer is final.
        if (participation.Status != ParticipationStatus.Invited)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.ParticipationAlreadyResponded);
        }

        if (HasPassed(participation.LiturgicalEvent))
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.ParticipationEventPassed);
        }

        participation.Status = request.Status;
        participation.Note = request.Note;
        participation.RespondedAt = DateTime.UtcNow;   // <-- UtcNow
        await participationRepository.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<EventParticipationDto>(participation);
        dto.MemberFullName = participation.Member.User.FullName;

        return Result<EventParticipationDto>.Success(dto);
    }

    // Event date and time are stored without a zone; they are compared with UTC now.
    private static bool HasPassed(LiturgicalEvent liturgicalEvent) =>
        liturgicalEvent.EventDate.ToDateTime(liturgicalEvent.Time) <= DateTime.UtcNow;   // <-- UtcNow

    /// <summary>
    /// Explicit ids must all exist, be active and not be invited yet. Without ids, everyone active
    /// who has no request yet is picked, so re-sending after new members join is safe.
    /// </summary>
    private async Task<Result<List<MemberProfile>>> ResolveInviteesAsync(
        Guid eventId, List<Guid>? requestedIds, CancellationToken cancellationToken)
    {
        if (requestedIds is { Count: > 0 })
        {
            var ids = requestedIds.Distinct().ToList();
            var members = await memberProfileRepository.GetByIdsWithUserAsync(ids, cancellationToken);
            if (members.Count != ids.Count)
            {
                return Result<List<MemberProfile>>.Failure(ErrorCodes.MemberNotFound);
            }

            if (members.Any(member => member.Status != MemberStatus.Active))
            {
                return Result<List<MemberProfile>>.Failure(ErrorCodes.MemberNotActive);
            }

            var alreadyRequested = await participationRepository.GetRequestedMemberIdsAsync(
                eventId, ids, cancellationToken);
            if (alreadyRequested.Count > 0)
            {
                return Result<List<MemberProfile>>.Failure(ErrorCodes.ParticipationAlreadyRequested);
            }

            return Result<List<MemberProfile>>.Success(members);
        }

        var active = await memberProfileRepository.GetAllActiveWithUserAsync(cancellationToken);
        var requested = (await participationRepository.GetRequestedMemberIdsAsync(
            eventId, active.Select(member => member.Id).ToList(), cancellationToken)).ToHashSet();

        return Result<List<MemberProfile>>.Success(active.Where(member => !requested.Contains(member.Id)).ToList());
    }
}
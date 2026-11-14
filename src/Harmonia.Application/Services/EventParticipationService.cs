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
    IChoirScopeService choirScope,
    ISystemSettingService systemSettingService,
    IServiceRosterRepository rosterRepository,
    IGenericRepository<Choir> choirRepository,
    IMapper mapper) : IEventParticipationService
{
    public async Task<Result<List<EventParticipationDto>>> SendRequestsAsync(
        SendParticipationRequestsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (await choirScope.CheckCanLeadEventAsync(
                liturgicalEvent.Id, ErrorCodes.LiturgicalEventNotFound, cancellationToken) is { } scopeError)
        {
            return Result<List<EventParticipationDto>>.Failure(scopeError);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        if (liturgicalEvent.Status != EventStatus.Published)
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.LiturgicalEventNotPublished);
        }

        if (HasPassed(liturgicalEvent))
        {
            return Result<List<EventParticipationDto>>.Failure(ErrorCodes.LiturgicalEventAlreadyPassed);
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
                Title: "Yêu cầu xác nhận tham gia",
                Content: string.Create(
                    CultureInfo.InvariantCulture,
                    $"Vui lòng xác nhận tham gia sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}."),
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
        if (liturgicalEvent is null || !await choirScope.CanViewEventAsync(eventId, cancellationToken))
        {
            return Result<EventParticipationSummaryDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
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

    public async Task<Result<EventParticipationSummaryDto>> ConfirmAllAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<EventParticipationSummaryDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (await choirScope.CheckCanLeadEventAsync(
                eventId, ErrorCodes.LiturgicalEventNotFound, cancellationToken) is { } scopeError)
        {
            return Result<EventParticipationSummaryDto>.Failure(scopeError);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<EventParticipationSummaryDto>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        // No "event has passed" check on purpose: the demo runs on past seeded events too.
        var invited = await participationRepository.GetInvitedForUpdateAsync(eventId, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var participation in invited)
        {
            participation.Status = ParticipationStatus.Confirmed;
            participation.RespondedAt = now;
        }

        await participationRepository.SaveChangesAsync(cancellationToken);

        return await GetByEventAsync(eventId, cancellationToken);
    }

    public async Task<Result<EventParticipationDto>> RespondAsync(
        Guid userId, Guid eventId, RespondParticipationRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.MemberNotFound);
        }

        // Looked up by the caller's own member id, so another member's request is never reachable.
        var participation = await participationRepository.GetByEventAndMemberAsync(
            eventId, profile.Id, cancellationToken);
        if (participation is null)
        {
            return Result<EventParticipationDto>.Failure(ErrorCodes.EventParticipationNotInvited);
        }

        var cutoffHours = await GetChangeCutoffHoursAsync(cancellationToken);
        if (CannotRespondCode(participation, participation.LiturgicalEvent, profile.Status, cutoffHours) is { } closedCode)
        {
            return Result<EventParticipationDto>.Failure(closedCode);
        }

        var withdrew = participation.Status != ParticipationStatus.Invited
            && participation.Status != ParticipationStatus.Declined
            && request.Status == ParticipationStatus.Declined;
        participation.Status = request.Status;
        participation.Note = request.Note;
        participation.RespondedAt = DateTime.UtcNow;
        var droppedLines = withdrew ? await DropRosterLinesAsync(eventId, profile.Id, cancellationToken) : 0;
        await participationRepository.SaveChangesAsync(cancellationToken);

        if (droppedLines > 0)
        {
            await NotifyLeadDirectorOfWithdrawalAsync(
                participation.LiturgicalEvent, participation.Member.User.FullName, cancellationToken);
        }

        var dto = mapper.Map<EventParticipationDto>(participation);
        dto.MemberFullName = participation.Member.User.FullName;

        return Result<EventParticipationDto>.Success(dto);
    }

    public async Task<Result<List<MyParticipationDto>>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<List<MyParticipationDto>>.Failure(ErrorCodes.MemberNotFound);
        }

        var participations = await participationRepository.GetUpcomingForMemberAsync(
            profile.Id, VietnamTime.Today, cancellationToken);
        var cutoffHours = await GetChangeCutoffHoursAsync(cancellationToken);

        return Result<List<MyParticipationDto>>.Success(participations
            .Select(p => ToMyParticipationDto(p, p.LiturgicalEvent, profile.Status, cutoffHours, mapper))
            .ToList());
    }

    /// <summary>Maps the member's own request and fills in whether <see cref="RespondAsync"/> would accept an answer now.</summary>
    internal static MyParticipationDto ToMyParticipationDto(
        EventParticipation participation, LiturgicalEvent liturgicalEvent, MemberStatus memberStatus, int changeCutoffHours,
        IMapper mapper)
    {
        var dto = mapper.Map<MyParticipationDto>(participation);
        dto.CannotRespondReason = CannotRespondCode(participation, liturgicalEvent, memberStatus, changeCutoffHours);
        dto.CanRespond = dto.CannotRespondReason is null;
        return dto;
    }

    // An Invited request can be answered until the event starts; an answer can be changed until changeCutoffHours before it.
    private static string? CannotRespondCode(
        EventParticipation participation, LiturgicalEvent liturgicalEvent, MemberStatus memberStatus, int changeCutoffHours)
    {
        if (liturgicalEvent.Status == EventStatus.Cancelled) return ErrorCodes.LiturgicalEventCancelled;
        if (memberStatus != MemberStatus.Active) return ErrorCodes.MemberNotActive;
        if (HasPassed(liturgicalEvent)) return ErrorCodes.EventParticipationClosed;
        var changeDeadline = liturgicalEvent.EventDate.ToDateTime(liturgicalEvent.Time).AddHours(-changeCutoffHours);
        return participation.Status != ParticipationStatus.Invited && changeDeadline <= VietnamTime.Now
            ? ErrorCodes.EventParticipationChangeClosed
            : null;
    }

    internal static Task<int> GetChangeCutoffHoursAsync(
        ISystemSettingService systemSettingService, CancellationToken cancellationToken) =>
        systemSettingService.GetIntAsync(SystemSettingKeys.ParticipationChangeCutoffHours, cancellationToken);

    private Task<int> GetChangeCutoffHoursAsync(CancellationToken cancellationToken) =>
        GetChangeCutoffHoursAsync(systemSettingService, cancellationToken);

    /// <summary>
    /// Removes the member's active lines on the event's roster and reopens it when finalized, so the lead director
    /// fills the gap. Saved with the caller's change; returns how many lines went.
    /// </summary>
    private async Task<int> DropRosterLinesAsync(Guid eventId, Guid memberId, CancellationToken cancellationToken)
    {
        var roster = (await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken))?.ServiceRoster;
        var lines = roster?.Assignments
            .Where(x => x.MemberId == memberId && x.Status == RosterAssignmentStatus.Active)
            .ToList() ?? [];
        if (roster is null || lines.Count == 0)
        {
            return 0;
        }

        foreach (var line in lines)
        {
            roster.Assignments.Remove(line);
        }

        if (roster.Status == RosterStatus.Finalized)
        {
            roster.Status = RosterStatus.Draft;
            roster.FinalizedAt = null;
            roster.FinalizedBy = null;
        }

        if (lines.Any(x => x.SongListItemId is null))
        {
            roster.WholeServiceFinalizedAt = null;
        }

        return lines.Count;
    }

    private async Task NotifyLeadDirectorOfWithdrawalAsync(
        LiturgicalEvent liturgicalEvent, string memberName, CancellationToken cancellationToken)
    {
        if (await choirScope.GetLeadChoirIdAsync(liturgicalEvent.Id, cancellationToken) is not { } leadChoirId
            || await choirRepository.GetByIdAsync(leadChoirId, cancellationToken) is not { DirectorId: { } directorId })
        {
            return;
        }

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.AssignmentNotice,
                Title: "Ca viên rút khỏi sự kiện",
                Content: $"{memberName} đã từ chối tham gia sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}; phân công của ca viên đã được gỡ, vui lòng xếp người thay.",
                RecipientUserIds: [directorId],
                ReferenceType: nameof(LiturgicalEvent),
                ReferenceId: liturgicalEvent.Id),
            cancellationToken);
    }

    // Event date and time are parish wall-clock values stored without a zone.
    private static bool HasPassed(LiturgicalEvent liturgicalEvent) =>
        liturgicalEvent.EventDate.ToDateTime(liturgicalEvent.Time) <= VietnamTime.Now;

    /// <summary>
    /// Explicit ids must all exist, be active and not be invited yet. Without ids, everyone active
    /// who has no request yet is picked, so re-sending after new members join is safe. When choirs serve the event
    /// (D8), only their members can be invited: others count as missing, and the default pick covers only them.
    /// </summary>
    private async Task<Result<List<MemberProfile>>> ResolveInviteesAsync(
        Guid eventId, List<Guid>? requestedIds, CancellationToken cancellationToken)
    {
        var eventMemberIds = await choirScope.GetEventMemberIdsAsync(eventId, cancellationToken);

        if (requestedIds is { Count: > 0 })
        {
            var ids = requestedIds.Distinct().ToList();
            var members = await memberProfileRepository.GetByIdsWithUserAsync(ids, cancellationToken);
            if (members.Count != ids.Count || (eventMemberIds is not null && ids.Any(id => !eventMemberIds.Contains(id))))
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
                return Result<List<MemberProfile>>.Failure(ErrorCodes.EventParticipationAlreadyRequested);
            }

            return Result<List<MemberProfile>>.Success(members);
        }

        var active = (await memberProfileRepository.GetAllActiveWithUserAsync(cancellationToken))
            .Where(member => eventMemberIds is null || eventMemberIds.Contains(member.Id))
            .ToList();
        var requested = (await participationRepository.GetRequestedMemberIdsAsync(
            eventId, active.Select(member => member.Id).ToList(), cancellationToken)).ToHashSet();

        return Result<List<MemberProfile>>.Success(active.Where(member => !requested.Contains(member.Id)).ToList());
    }
}
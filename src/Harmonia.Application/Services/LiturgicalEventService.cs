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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class LiturgicalEventService(
    ILiturgicalEventRepository liturgicalEventRepository,
    IUserRepository userRepository,
    IGenericRepository<Choir> choirRepository,
    INotificationService notificationService,
    ILookupService lookupService,
    IChoirScopeService choirScope,
    ISystemSettingService systemSettingService,
    ICurrentUserService currentUser,
    IRosterService rosterService,
    IEventParticipationRepository participationRepository,
    IServiceRosterRepository rosterRepository,
    IMapper mapper) : ILiturgicalEventService
{
    public async Task<Result<PagedList<LiturgicalEventListItemDto>>> SearchAsync(
        SearchLiturgicalEventsRequest request, bool includeDrafts, CancellationToken cancellationToken)
    {
        var choirIds = await choirScope.GetDirectedChoirIdsAsync(cancellationToken);
        var page = await liturgicalEventRepository.SearchAsync(
            request, includeDrafts, choirIds, currentUser.UserId, cancellationToken);
        var leadDays = await systemSettingService.GetIntAsync(SystemSettingKeys.SongListLeadDays, cancellationToken);

        var items = mapper.Map<List<LiturgicalEventListItemDto>>(page.Items);
        var today = VietnamTime.Today;
        foreach (var item in items)
        {
            item.SongListDueDate = item.EventDate.AddDays(-leadDays);
            item.IsSongListOverdue = LiturgicalEvent.IsSongListOverdue(
                item.EventDate, item.Status, leadDays, item.LatestSongListStatus, today);
        }

        return Result<PagedList<LiturgicalEventListItemDto>>.Success(new PagedList<LiturgicalEventListItemDto>(
            items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<LiturgicalEventDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);

        return liturgicalEvent is null
            ? Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventNotFound)
            : Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    public async Task<Result<LiturgicalEventDto>> CreateAsync(
        CreateLiturgicalEventRequest request, CancellationToken cancellationToken)
    {
        // Same rule as events generated from Mass schedules: no season picked → the season covering the date.
        var seasonId = request.LiturgicalSeasonId ?? await lookupService.FindLiturgicalSeasonIdAsync(request.EventDate, cancellationToken);

        var catalogs = await lookupService.CheckEventCatalogsAsync(
            request.EventDate, seasonId, request.MassTypeId, request.CeremonyTypeId,
            request.CategoryId, request.LocationId, null, cancellationToken);
        if (!catalogs.IsSuccess)
        {
            return Result<LiturgicalEventDto>.Failure(catalogs.Code!);
        }

        var choirIds = request.ChoirIds.Distinct().ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);
        if (choirs.Count != choirIds.Count)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.ChoirNotFound);
        }

        if (choirs.Any(x => !x.IsActive))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.ChoirInactive);
        }

        if (await liturgicalEventRepository.ExistsBySlotAsync(
                request.EventDate, request.Time, request.LocationId, null, cancellationToken))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventSlotTaken);
        }

        var eventId = Guid.NewGuid();
        var liturgicalEvent = new LiturgicalEvent
        {
            Id = eventId,
            EventDate = request.EventDate,
            Time = request.Time,
            LiturgicalSeasonId = seasonId,
            MassTypeId = request.MassTypeId,
            CeremonyTypeId = request.CeremonyTypeId,
            CategoryId = request.CategoryId,
            LocationId = request.LocationId,
            Title = request.Title,
            SpecialRequirements = request.SpecialRequirements,
            Status = EventStatus.Draft,
            EventChoirs = choirIds.Select(choirId => new LiturgicalEventChoir
            {
                Id = Guid.NewGuid(), EventId = eventId, ChoirId = choirId, IsLead = choirId == request.LeadChoirId,
            }).ToList(),
        };

        await liturgicalEventRepository.AddAsync(liturgicalEvent, cancellationToken);
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    public async Task<Result<LiturgicalEventDto>> UpdateAsync(
        Guid id, UpdateLiturgicalEventRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        var catalogs = await lookupService.CheckEventCatalogsAsync(
            request.EventDate, request.LiturgicalSeasonId, request.MassTypeId, request.CeremonyTypeId,
            request.CategoryId, request.LocationId, liturgicalEvent, cancellationToken);
        if (!catalogs.IsSuccess)
        {
            return Result<LiturgicalEventDto>.Failure(catalogs.Code!);
        }

        if (await liturgicalEventRepository.ExistsBySlotAsync(
                request.EventDate, request.Time, request.LocationId, id, cancellationToken))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventSlotTaken);
        }

        var isPublished = liturgicalEvent.Status == EventStatus.Published;
        var dateChanged = liturgicalEvent.EventDate != request.EventDate;
        var rescheduled = isPublished
            && (dateChanged || liturgicalEvent.Time != request.Time || liturgicalEvent.LocationId != request.LocationId);
        if (rescheduled && await rosterService.HasAssignedMemberTimeConflictAsync(
                id, request.EventDate, request.Time, cancellationToken))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.RosterMemberTimeConflict);
        }

        var previousLabel = $"{liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm} tại {liturgicalEvent.Location?.Name}";
        if (isPublished && dateChanged)
        {
            await ResetForNewDateAsync(id, cancellationToken);
        }

        liturgicalEvent.EventDate = request.EventDate;
        liturgicalEvent.Time = request.Time;
        liturgicalEvent.LiturgicalSeasonId = request.LiturgicalSeasonId;
        liturgicalEvent.MassTypeId = request.MassTypeId;
        liturgicalEvent.CeremonyTypeId = request.CeremonyTypeId;
        liturgicalEvent.CategoryId = request.CategoryId;
        liturgicalEvent.LocationId = request.LocationId;
        liturgicalEvent.Title = request.Title;
        liturgicalEvent.SpecialRequirements = request.SpecialRequirements;
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        // Reload so LocationName follows a changed LocationId.
        var updated = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);
        if (rescheduled)
        {
            var reconfirm = dateChanged ? " Vui lòng xác nhận tham gia lại." : string.Empty;
            await NotifyDirectorsAndMembersAsync(
                updated!,
                NotificationType.EventRescheduled,
                "Sự kiện đã đổi thời gian hoặc địa điểm",
                $"Sự kiện ngày {previousLabel} chuyển sang ngày {updated!.EventDate:dd/MM/yyyy} lúc {updated.Time:HH:mm} tại {updated.Location?.Name}.{reconfirm}",
                cancellationToken);
        }

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(updated));
    }

    /// <summary>
    /// A new date voids every answer, so members are asked again and a finalized roster reopens; its lines stay, and
    /// finalizing again needs the members to confirm the new date. Saved with the caller's change.
    /// </summary>
    private async Task ResetForNewDateAsync(Guid eventId, CancellationToken cancellationToken)
    {
        foreach (var participation in await participationRepository.GetRespondedForUpdateAsync(eventId, cancellationToken))
        {
            participation.Status = ParticipationStatus.Invited;
            participation.RespondedAt = null;
        }

        var roster = (await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken))?.ServiceRoster;
        if (roster is { Status: RosterStatus.Finalized })
        {
            roster.Status = RosterStatus.Draft;
            roster.FinalizedAt = null;
            roster.FinalizedBy = null;
        }

        if (roster is not null)
        {
            roster.WholeServiceFinalizedAt = null;
        }
    }

    public async Task<Result<LiturgicalEventDto>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(id, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Published)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventAlreadyPublished);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        var incomplete = await CheckPublishableAsync(liturgicalEvent, cancellationToken);
        if (incomplete is not null)
        {
            return Result<LiturgicalEventDto>.Failure(incomplete);
        }

        liturgicalEvent.Status = EventStatus.Published;
        liturgicalEvent.PublishedAt = DateTime.UtcNow;
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        await NotifyDirectorsAndMembersAsync(
            liturgicalEvent,
            NotificationType.EventPublished,
            "Sự kiện mới được công bố",
            $"Sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm} đã được công bố.",
            cancellationToken);
        await NotifyAssignedDirectorsAsync(liturgicalEvent, cancellationToken);

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    public async Task<Result<LiturgicalEventDto>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        if (liturgicalEvent.EventDate < VietnamTime.Today)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.LiturgicalEventAlreadyPassed);
        }

        var wasPublished = liturgicalEvent.Status == EventStatus.Published;
        liturgicalEvent.Status = EventStatus.Cancelled;
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        // A Draft was never shown to anyone, so only a published event needs a notice.
        if (wasPublished)
        {
            await NotifyDirectorsAndMembersAsync(
                liturgicalEvent,
                NotificationType.EventCancelled,
                "Sự kiện đã bị huỷ",
                $"Sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm} đã bị huỷ.",
                cancellationToken);
        }

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    /// <summary>
    /// A published event must be complete and still valid: not past, with a type, a season and a lead choir, every
    /// catalog active (a row switched off since the last save no longer passes) and the date + time + location still free.
    /// </summary>
    private async Task<string?> CheckPublishableAsync(LiturgicalEvent liturgicalEvent, CancellationToken cancellationToken)
    {
        if (liturgicalEvent.EventDate < VietnamTime.Today) return ErrorCodes.LiturgicalEventAlreadyPassed;
        if (liturgicalEvent.MassTypeId is null && liturgicalEvent.CeremonyTypeId is null)
        {
            return ErrorCodes.LiturgicalEventTypeRequired;
        }

        if (liturgicalEvent.LiturgicalSeasonId is null) return ErrorCodes.LiturgicalEventSeasonRequired;

        // Generated drafts have no choir yet, and only a lead choir may plan a published event.
        if (await choirScope.GetLeadChoirIdAsync(liturgicalEvent.Id, cancellationToken) is null)
        {
            return ErrorCodes.LiturgicalEventChoirRequired;
        }

        var catalogs = await lookupService.CheckEventCatalogsAsync(
            liturgicalEvent.EventDate, liturgicalEvent.LiturgicalSeasonId, liturgicalEvent.MassTypeId,
            liturgicalEvent.CeremonyTypeId, liturgicalEvent.CategoryId, liturgicalEvent.LocationId, null,
            cancellationToken);
        if (!catalogs.IsSuccess) return catalogs.Code;

        return await liturgicalEventRepository.ExistsBySlotAsync(
                liturgicalEvent.EventDate, liturgicalEvent.Time, liturgicalEvent.LocationId, liturgicalEvent.Id,
                cancellationToken)
            ? ErrorCodes.LiturgicalEventSlotTaken
            : null;
    }

    /// <summary>
    /// Tells each serving choir's director whether the choir leads or serves. Sent on publish, not when choirs are
    /// picked: a Draft's choirs may still change.
    /// </summary>
    private async Task NotifyAssignedDirectorsAsync(LiturgicalEvent liturgicalEvent, CancellationToken cancellationToken)
    {
        var leadId = await choirScope.GetLeadChoirIdAsync(liturgicalEvent.Id, cancellationToken);
        var choirs = await choirRepository.ListAsync(
            x => x.EventChoirs.Any(e => e.EventId == liturgicalEvent.Id), cancellationToken);

        foreach (var choir in choirs.Where(x => x.DirectorId is not null))
        {
            var role = choir.Id == leadId ? "phụ trách" : "phục vụ";
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    Type: NotificationType.EventChoirChanged,
                    Title: $"Ca đoàn được giao {role} sự kiện",
                    Content: $"Ca đoàn {choir.Name} được giao {role} sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}.",
                    RecipientUserIds: [choir.DirectorId!.Value],
                    ReferenceType: nameof(LiturgicalEvent),
                    ReferenceId: liturgicalEvent.Id),
                cancellationToken);
        }
    }

    private async Task NotifyDirectorsAndMembersAsync(
        LiturgicalEvent liturgicalEvent, NotificationType type, string title, string content, CancellationToken cancellationToken)
    {
        // An event with choirs concerns only them (D8); one without stays a parish-wide announcement.
        var recipientUserIds = await userRepository.GetActiveUserIdsOfEventChoirsAsync(liturgicalEvent.Id, cancellationToken)
            ?? await userRepository.GetActiveUserIdsByRolesAsync(
                [RoleNames.ChoirDirector, RoleNames.ChoirMember], cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: type,
                Title: title,
                Content: content,
                RecipientUserIds: recipientUserIds,
                ReferenceType: nameof(LiturgicalEvent),
                ReferenceId: liturgicalEvent.Id),
            cancellationToken);
    }
}

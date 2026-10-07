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
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class LiturgicalEventService(
    ILiturgicalEventRepository liturgicalEventRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IMapper mapper) : ILiturgicalEventService
{
    public async Task<Result<PagedList<LiturgicalEventDto>>> SearchAsync(
        SearchLiturgicalEventsRequest request, CancellationToken cancellationToken)
    {
        var page = await liturgicalEventRepository.SearchAsync(request, cancellationToken);

        return Result<PagedList<LiturgicalEventDto>>.Success(new PagedList<LiturgicalEventDto>(
            mapper.Map<List<LiturgicalEventDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<LiturgicalEventDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);

        return liturgicalEvent is null
            ? Result<LiturgicalEventDto>.Failure(ErrorCodes.EventNotFound)
            : Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    public async Task<Result<LiturgicalEventDto>> CreateAsync(
        CreateLiturgicalEventRequest request, CancellationToken cancellationToken)
    {
        if (await liturgicalEventRepository.ExistsBySlotAsync(
                request.EventDate, request.Time, request.LocationId, null, cancellationToken))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventSlotTaken);
        }

        var liturgicalEvent = new LiturgicalEvent
        {
            Id = Guid.NewGuid(),
            EventDate = request.EventDate,
            Time = request.Time,
            LiturgicalSeasonId = request.LiturgicalSeasonId,
            MassTypeId = request.MassTypeId,
            CeremonyTypeId = request.CeremonyTypeId,
            CategoryId = request.CategoryId,
            LocationId = request.LocationId,
            Title = request.Title,
            SpecialRequirements = request.SpecialRequirements,
            Status = EventStatus.Draft,
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
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventCancelled);
        }

        if (await liturgicalEventRepository.ExistsBySlotAsync(
                request.EventDate, request.Time, request.LocationId, id, cancellationToken))
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventSlotTaken);
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
        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(updated));
    }

    public async Task<Result<LiturgicalEventDto>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(id, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Published)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventAlreadyPublished);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventCancelled);
        }

        liturgicalEvent.Status = EventStatus.Published;
        liturgicalEvent.PublishedAt = DateTime.UtcNow;
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        await NotifyDirectorsAndMembersAsync(
            liturgicalEvent,
            NotificationType.EventPublished,
            "New event published",
            $"The event on {liturgicalEvent.EventDate:dd/MM/yyyy} at {liturgicalEvent.Time:HH:mm} has been published.",
            cancellationToken);

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    public async Task<Result<LiturgicalEventDto>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithLocationAsync(id, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventCancelled);
        }

        if (liturgicalEvent.EventDate < VietnamTime.Today)
        {
            return Result<LiturgicalEventDto>.Failure(ErrorCodes.EventAlreadyPassed);
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
                "Event cancelled",
                $"The event on {liturgicalEvent.EventDate:dd/MM/yyyy} at {liturgicalEvent.Time:HH:mm} has been cancelled.",
                cancellationToken);
        }

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }

    private async Task NotifyDirectorsAndMembersAsync(
        LiturgicalEvent liturgicalEvent, NotificationType type, string title, string content, CancellationToken cancellationToken)
    {
        var recipientUserIds = await userRepository.GetActiveUserIdsByRolesAsync(
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

using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Services;

public class LiturgicalEventService(
    ILiturgicalEventRepository liturgicalEventRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IMapper mapper) : ILiturgicalEventService
{
    public async Task<Result<LiturgicalEventDto>> CreateAsync(
        CreateLiturgicalEventRequest request, CancellationToken cancellationToken)
    {
        if (await liturgicalEventRepository.ExistsBySlotAsync(
                request.EventDate, request.Time, request.LocationId, cancellationToken))
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
        liturgicalEvent.PublishedAt = DateTime.Now;
        await liturgicalEventRepository.SaveChangesAsync(cancellationToken);

        var recipientUserIds = await userRepository.GetActiveUserIdsByRolesAsync(
            [RoleNames.ChoirDirector, RoleNames.ChoirMember], cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.EventPublished,
                Title: "New event published",
                Content: $"The event on {liturgicalEvent.EventDate:dd/MM/yyyy} at {liturgicalEvent.Time:HH:mm} has been published.",
                RecipientUserIds: recipientUserIds,
                ReferenceType: nameof(LiturgicalEvent),
                ReferenceId: liturgicalEvent.Id),
            cancellationToken);

        return Result<LiturgicalEventDto>.Success(mapper.Map<LiturgicalEventDto>(liturgicalEvent));
    }
}
using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    INotificationPublisher notificationPublisher,
    IMapper mapper) : INotificationService
{
    public async Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var recipientUserIds = request.RecipientUserIds.Distinct().ToList();
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Title = request.Title,
            Content = request.Content,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            CreatedAt = DateTime.UtcNow,
            Recipients = recipientUserIds
                .Select(userId => new NotificationRecipient { Id = Guid.NewGuid(), UserId = userId })
                .ToList(),
        };

        // Store before pushing, so a client that refetches after the push sees the same row.
        await notificationRepository.AddAsync(notification, cancellationToken);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        await notificationPublisher.PublishAsync(
            mapper.Map<NotificationDto>(notification), recipientUserIds, cancellationToken);
    }

    public async Task<Result<PagedList<NotificationDto>>> GetForUserAsync(
        Guid userId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var page = await notificationRepository.GetForUserAsync(userId, paging, cancellationToken);

        return Result<PagedList<NotificationDto>>.Success(new PagedList<NotificationDto>(
            mapper.Map<List<NotificationDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<int>> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Result<int>.Success(await notificationRepository.CountUnreadAsync(userId, cancellationToken));

    public async Task<Result> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var recipient = await notificationRepository.GetRecipientAsync(notificationId, userId, cancellationToken);
        if (recipient is null)
        {
            // Another user's notification reads as missing, so its existence stays hidden.
            return Result.Failure(ErrorCodes.NotificationNotFound);
        }

        if (!recipient.IsRead)
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await notificationRepository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

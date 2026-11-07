using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class DirectorNoteService(
    IDirectorNoteRepository directorNoteRepository,
    IUserRepository userRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    INotificationService notificationService,
    INotificationPublisher notificationPublisher,
    IMapper mapper) : IDirectorNoteService
{
    private const int NotificationPreviewLength = 200;

    public async Task<Result<List<UserSummaryDto>>> GetRecipientsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var directorIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ChoirDirector], cancellationToken);
        var recipientRole = directorIds.Contains(userId) ? RoleNames.ParishPriest : RoleNames.ChoirDirector;
        var recipients = await userRepository.GetActiveByRoleAsync(recipientRole, cancellationToken);

        return Result<List<UserSummaryDto>>.Success(mapper.Map<List<UserSummaryDto>>(recipients));
    }

    public async Task<Result<List<DirectorNoteDto>>> CreateAsync(
        Guid fromUserId, CreateDirectorNoteRequest request, CancellationToken cancellationToken)
    {
        var recipientIds = request.ToUserIds.Distinct().ToList();
        var directorIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ChoirDirector], cancellationToken);
        if (recipientIds.Except(directorIds).Any())
        {
            return Result<List<DirectorNoteDto>>.Failure(ErrorCodes.DirectorNoteRecipientInvalid);
        }

        if (request.EventId is { } eventId
            && await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken) is null)
        {
            return Result<List<DirectorNoteDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        var content = request.Content.Trim();
        var sentAt = DateTime.UtcNow;
        var notes = recipientIds.Select(toUserId => new DirectorNote
        {
            Id = Guid.NewGuid(),
            NoteDate = request.NoteDate,
            EventId = request.EventId,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Content = content,
            SentAt = sentAt,
        }).ToList();

        foreach (var note in notes) await directorNoteRepository.AddAsync(note, cancellationToken);
        await directorNoteRepository.SaveChangesAsync(cancellationToken);

        // One notification per director: each points at that director's own copy of the note.
        var preview = content.Length <= NotificationPreviewLength ? content : content[..NotificationPreviewLength] + "...";
        foreach (var note in notes)
        {
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.DirectorNote, "Ghi chú mới từ cha xứ", preview,
                    [note.ToUserId], nameof(DirectorNote), note.Id),
                cancellationToken);
        }

        var saved = new List<DirectorNoteDto>();
        foreach (var note in notes)
        {
            var dto = mapper.Map<DirectorNoteDto>(await directorNoteRepository.GetWithDetailsAsync(note.Id, cancellationToken));
            await notificationPublisher.PublishDirectorNoteAsync(dto, [dto.ToUserId, dto.FromUserId], cancellationToken);
            saved.Add(dto);
        }

        return Result<List<DirectorNoteDto>>.Success(saved);
    }

    public async Task<Result<DirectorNoteDto>> SendMessageAsync(
        Guid fromUserId, SendDirectorNoteMessageRequest request, CancellationToken cancellationToken)
    {
        var priestIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ParishPriest], cancellationToken);
        var directorIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ChoirDirector], cancellationToken);
        var isAllowedPair = (priestIds.Contains(fromUserId) && directorIds.Contains(request.ToUserId))
            || (directorIds.Contains(fromUserId) && priestIds.Contains(request.ToUserId));
        if (!isAllowedPair)
        {
            return Result<DirectorNoteDto>.Failure(ErrorCodes.DirectorNoteRecipientInvalid);
        }

        var note = new DirectorNote
        {
            Id = Guid.NewGuid(),
            FromUserId = fromUserId,
            ToUserId = request.ToUserId,
            Content = request.Content.Trim(),
            SentAt = DateTime.UtcNow,
        };
        await directorNoteRepository.AddAsync(note, cancellationToken);
        await directorNoteRepository.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<DirectorNoteDto>(await directorNoteRepository.GetWithDetailsAsync(note.Id, cancellationToken));
        // The sender gets it too, so their other open tabs show the message.
        await notificationPublisher.PublishDirectorNoteAsync(dto, [note.ToUserId, note.FromUserId], cancellationToken);

        return Result<DirectorNoteDto>.Success(dto);
    }

    public async Task<Result<PagedList<DirectorNoteDto>>> SearchAsync(
        Guid userId, SearchDirectorNotesRequest request, CancellationToken cancellationToken)
    {
        var page = await directorNoteRepository.SearchForUserAsync(userId, request, cancellationToken);

        return Result<PagedList<DirectorNoteDto>>.Success(new PagedList<DirectorNoteDto>(
            mapper.Map<List<DirectorNoteDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<List<DirectorNoteConversationDto>>> GetConversationsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var conversations = await directorNoteRepository.GetConversationsAsync(userId, cancellationToken);

        return Result<List<DirectorNoteConversationDto>>.Success(conversations.Select(c =>
        {
            var other = c.LastNote.FromUserId == userId ? c.LastNote.ToUser : c.LastNote.FromUser;
            return new DirectorNoteConversationDto
            {
                UserId = other.Id,
                FullName = other.FullName,
                LastNote = mapper.Map<DirectorNoteDto>(c.LastNote),
                UnreadCount = c.UnreadCount,
            };
        }).ToList());
    }

    public async Task<Result> MarkConversationReadAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var readAt = DateTime.UtcNow;
        var changed = await directorNoteRepository.MarkReadAsync(userId, otherUserId, readAt, cancellationToken);

        // Nothing changed covers an unknown user too, answered the same way so it reveals nothing.
        if (changed > 0)
        {
            await notificationPublisher.PublishDirectorNotesReadAsync(
                new DirectorNotesReadDto { ReaderUserId = userId, SenderUserId = otherUserId, ReadAt = readAt },
                [otherUserId, userId], cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<DirectorNoteDto>> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var note = await directorNoteRepository.GetWithDetailsAsync(id, cancellationToken);

        // A note between other people reads as missing rather than forbidden.
        if (note is null || (note.FromUserId != userId && note.ToUserId != userId))
        {
            return Result<DirectorNoteDto>.Failure(ErrorCodes.DirectorNoteNotFound);
        }

        return Result<DirectorNoteDto>.Success(mapper.Map<DirectorNoteDto>(note));
    }
}

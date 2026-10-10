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
    IMapper mapper) : IDirectorNoteService
{
    private const int NotificationPreviewLength = 200;

    public async Task<Result<List<UserSummaryDto>>> GetRecipientsAsync(CancellationToken cancellationToken)
    {
        var directors = await userRepository.GetActiveByRoleAsync(RoleNames.ChoirDirector, cancellationToken);

        return Result<List<UserSummaryDto>>.Success(mapper.Map<List<UserSummaryDto>>(directors));
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
            return Result<List<DirectorNoteDto>>.Failure(ErrorCodes.EventNotFound);
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
                    NotificationType.DirectorNote, "New note from the parish priest", preview,
                    [note.ToUserId], nameof(DirectorNote), note.Id),
                cancellationToken);
        }

        var saved = new List<DirectorNoteDto>();
        foreach (var note in notes)
        {
            saved.Add(mapper.Map<DirectorNoteDto>(await directorNoteRepository.GetWithDetailsAsync(note.Id, cancellationToken)));
        }

        return Result<List<DirectorNoteDto>>.Success(saved);
    }

    public async Task<Result<PagedList<DirectorNoteDto>>> SearchAsync(
        Guid userId, SearchDirectorNotesRequest request, CancellationToken cancellationToken)
    {
        var page = await directorNoteRepository.SearchForUserAsync(userId, request, cancellationToken);

        return Result<PagedList<DirectorNoteDto>>.Success(new PagedList<DirectorNoteDto>(
            mapper.Map<List<DirectorNoteDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
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

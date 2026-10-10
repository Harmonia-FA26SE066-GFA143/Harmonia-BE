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

public class SongListService(
    ISongListRepository songListRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IGenericRepository<Song> songRepository,
    IGenericRepository<LiturgicalSlot> slotRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IMapper mapper) : ISongListService
{
    public async Task<Result<SongListDto>> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        // Every role can call this, so a list must not outlive the visibility of its event (e.g. cancelled later).
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (liturgicalEvent is null || liturgicalEvent.Status != EventStatus.Published)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotFound);
        }

        var songList = await songListRepository.GetApprovedForEventAsync(eventId, cancellationToken);

        return songList is null
            ? Result<SongListDto>.Failure(ErrorCodes.SongListNotFound)
            : Result<SongListDto>.Success(mapper.Map<SongListDto>(songList));
    }

    public async Task<Result<SongListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var songList = await songListRepository.GetByIdWithDetailsAsync(id, cancellationToken);

        return songList is null
            ? Result<SongListDto>.Failure(ErrorCodes.SongListNotFound)
            : Result<SongListDto>.Success(mapper.Map<SongListDto>(songList));
    }

    public async Task<Result<List<SongListDto>>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await songListRepository.GetByStatusAsync(SongListStatus.Submitted, cancellationToken);

        return Result<List<SongListDto>>.Success(mapper.Map<List<SongListDto>>(pending));
    }

    public async Task<Result<SongListDto>> CreateAsync(
        CreateSongListRequest request, Guid proposedBy, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<SongListDto>.Failure(ErrorCodes.EventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<SongListDto>.Failure(ErrorCodes.EventCancelled);
        }

        // Only the Parish Priest sees a Draft event; every other role sees it once it is published.
        if (liturgicalEvent.Status != EventStatus.Published)
        {
            return Result<SongListDto>.Failure(ErrorCodes.EventNotPublished);
        }

        var latest = await songListRepository.GetLatestVersionAsync(request.EventId, cancellationToken);

        if (latest is not null)
        {
            if (latest.Status == SongListStatus.Approved)
            {
                return Result<SongListDto>.Failure(ErrorCodes.SongListEventHasApprovedVersion);
            }

            if (latest.Status != SongListStatus.Rejected && latest.Status != SongListStatus.NeedsRevision)
            {
                return Result<SongListDto>.Failure(ErrorCodes.SongListCannotBeRevised);
            }
        }

        var itemsError = await ValidateItemReferencesAsync(request.Items, cancellationToken);
        if (itemsError is not null)
        {
            return Result<SongListDto>.Failure(itemsError);
        }

        var songList = new SongList
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            Version = (latest?.Version ?? 0) + 1,
            Status = SongListStatus.Draft,
            ProposedBy = proposedBy,
            PreviousVersionId = latest?.Id,
            // Saved together with the list: one transaction, so a failure cannot leave an empty draft behind.
            Items = request.Items.Select(i => new SongListItem
            {
                Id = Guid.NewGuid(),
                SongId = i.SongId,
                SlotId = i.SlotId,
                DisplayOrder = i.DisplayOrder,
                Note = i.Note,
            }).ToList(),
        };

        await songListRepository.AddAsync(songList, cancellationToken);
        await songListRepository.SaveChangesAsync(cancellationToken);

        var created = await songListRepository.GetByIdWithDetailsAsync(songList.Id, cancellationToken);
        return Result<SongListDto>.Success(mapper.Map<SongListDto>(created));
    }

    public async Task<Result<SongListDto>> UpdateItemsAsync(
        Guid id, UpdateSongListItemsRequest request, CancellationToken cancellationToken)
    {
        var songList = await songListRepository.GetByIdAsync(id, cancellationToken);
        if (songList is null)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotFound);
        }

        var latest = await songListRepository.GetLatestVersionAsync(songList.EventId, cancellationToken);
        if (latest is null || latest.Id != songList.Id)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotLatestVersion);
        }

        if (songList.Status != SongListStatus.Draft)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotEditable);
        }

        var itemsError = await ValidateItemReferencesAsync(request.Items, cancellationToken);
        if (itemsError is not null)
        {
            return Result<SongListDto>.Failure(itemsError);
        }

        var existingItems = await songListRepository.GetItemsAsync(id, cancellationToken);
        await songListRepository.RemoveItemsAsync(existingItems, cancellationToken);

        var newItems = request.Items.Select(i => new SongListItem
        {
            Id = Guid.NewGuid(),
            SongListId = id,
            SongId = i.SongId,
            SlotId = i.SlotId,
            DisplayOrder = i.DisplayOrder,
            Note = i.Note,
        });

        await songListRepository.AddItemsAsync(newItems, cancellationToken);
        await songListRepository.SaveChangesAsync(cancellationToken);

        var updated = await songListRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return Result<SongListDto>.Success(mapper.Map<SongListDto>(updated));
    }

    public async Task<Result<SongListDto>> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        var songList = await songListRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (songList is null)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotFound);
        }

        var latest = await songListRepository.GetLatestVersionAsync(songList.EventId, cancellationToken);
        if (latest is null || latest.Id != songList.Id)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotLatestVersion);
        }

        if (songList.Status == SongListStatus.Submitted)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListAlreadySubmitted);
        }

        if (songList.Status != SongListStatus.Draft)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotEditable);
        }

        if (songList.Items.Count == 0)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListEmpty);
        }

        songList.Status = SongListStatus.Submitted;
        songList.SubmittedAt = DateTime.UtcNow;
        await songListRepository.SaveChangesAsync(cancellationToken);

        var parishPriestIds = await userRepository.GetActiveUserIdsByRolesAsync(
            [RoleNames.ParishPriest], cancellationToken);

        if (parishPriestIds.Count > 0)
        {
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    Type: NotificationType.SongListSubmitted,
                    Title: "Song list awaiting review",
                    Content: $"Song list version {songList.Version} has been submitted for review.",
                    RecipientUserIds: parishPriestIds,
                    ReferenceType: nameof(SongList),
                    ReferenceId: songList.Id),
                cancellationToken);
        }

        return Result<SongListDto>.Success(mapper.Map<SongListDto>(songList));
    }

    public async Task<Result<SongListDto>> ReviewAsync(
        Guid id, ReviewSongListRequest request, Guid reviewerId, CancellationToken cancellationToken)
    {
        var songList = await songListRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (songList is null)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotFound);
        }

        if (songList.Status != SongListStatus.Submitted)
        {
            return Result<SongListDto>.Failure(ErrorCodes.SongListNotSubmitted);
        }

        songList.Status = request.Decision switch
        {
            ReviewDecision.Approve => SongListStatus.Approved,
            ReviewDecision.Reject => SongListStatus.Rejected,
            ReviewDecision.RequestRevision => SongListStatus.NeedsRevision,
            _ => songList.Status,
        };
        songList.DecidedAt = DateTime.UtcNow;

        var review = new SongListReview
        {
            Id = Guid.NewGuid(),
            SongListId = songList.Id,
            ReviewerId = reviewerId,
            Decision = request.Decision,
            Notes = request.Notes,
            ReviewedAt = DateTime.UtcNow,
        };
        await songListRepository.AddReviewAsync(review, cancellationToken);
        await songListRepository.SaveChangesAsync(cancellationToken);

        var decisionText = request.Decision switch
        {
            ReviewDecision.Approve => "has been approved",
            ReviewDecision.Reject => "has been rejected",
            ReviewDecision.RequestRevision => "needs revision",
            _ => "has been reviewed",
        };

        await notificationService.SendAsync(
            new SendNotificationRequest(
                Type: NotificationType.SongListDecision,
                Title: "Song list reviewed",
                Content: $"Song list version {songList.Version} {decisionText}.",
                RecipientUserIds: [songList.ProposedBy],
                ReferenceType: nameof(SongList),
                ReferenceId: songList.Id),
            cancellationToken);

        var result = await songListRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return Result<SongListDto>.Success(mapper.Map<SongListDto>(result));
    }

    private async Task<string?> ValidateItemReferencesAsync(
        List<UpdateSongListItemRequest> items, CancellationToken cancellationToken)
    {
        foreach (var songId in items.Select(i => i.SongId).Distinct())
        {
            var song = await songRepository.GetByIdAsync(songId, cancellationToken);
            if (song is null)
            {
                return ErrorCodes.SongNotFound;
            }

            if (!song.IsActive)
            {
                return ErrorCodes.SongInactive;
            }
        }

        foreach (var slotId in items.Select(i => i.SlotId).Distinct())
        {
            var slot = await slotRepository.GetByIdAsync(slotId, cancellationToken);
            if (slot is null)
            {
                return ErrorCodes.SlotNotFound;
            }

            if (!slot.IsActive)
            {
                return ErrorCodes.LookupInactive;
            }
        }

        return null;
    }
}
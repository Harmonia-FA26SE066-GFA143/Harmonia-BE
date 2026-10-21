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

public class RehearsalService(
    IRehearsalRepository rehearsalRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IGenericRepository<WorshipLocation> locationRepository,
    IGenericRepository<Song> songRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IMapper mapper) : IRehearsalService
{
    public async Task<Result<PagedList<RehearsalDto>>> SearchAsync(
        SearchRehearsalsRequest request, CancellationToken cancellationToken)
    {
        var page = await rehearsalRepository.SearchAsync(request, cancellationToken);

        return Result<PagedList<RehearsalDto>>.Success(new PagedList<RehearsalDto>(
            mapper.Map<List<RehearsalDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<RehearsalDto>> CreateAsync(
        CreateRehearsalRequest request, CancellationToken cancellationToken)
    {
        if (await CheckEventAsync(request.EventId, cancellationToken) is { } eventError)
        {
            return Result<RehearsalDto>.Failure(eventError);
        }

        var (location, locationError) = await CheckLocationAsync(
            request.LocationId, null, request.StartTime, request.EndTime, null, cancellationToken);
        if (locationError is not null)
        {
            return Result<RehearsalDto>.Failure(locationError);
        }

        var songIds = request.Songs.Select(i => i.SongId).ToList();
        var songs = await songRepository.ListAsync(x => songIds.Contains(x.Id), cancellationToken);
        if (songs.Count != songIds.Count)
        {
            return Result<RehearsalDto>.Failure(ErrorCodes.SongNotFound);
        }

        if (songs.Any(song => !song.IsActive))
        {
            return Result<RehearsalDto>.Failure(ErrorCodes.SongInactive);
        }

        var rehearsalSongs = request.Songs
            .Select((item, index) => new RehearsalSong
            {
                Id = Guid.NewGuid(),
                SongId = item.SongId,
                DisplayOrder = index + 1,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
            })
            .ToList();

        var rehearsal = new Rehearsal
        {
            Id = Guid.NewGuid(),
            EventId = request.EventId,
            LocationId = request.LocationId,
            Location = location,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Note = request.Note,
            Songs = rehearsalSongs,
        };

        await rehearsalRepository.AddAsync(rehearsal, cancellationToken);
        await rehearsalRepository.SaveChangesAsync(cancellationToken);

        // Songs come from an untracked query; attached before saving, EF would insert them again as new rows.
        var songsById = songs.ToDictionary(song => song.Id);
        foreach (var rehearsalSong in rehearsalSongs)
        {
            rehearsalSong.Song = songsById[rehearsalSong.SongId];
        }

        return Result<RehearsalDto>.Success(mapper.Map<RehearsalDto>(rehearsal));
    }

    public async Task<Result<RehearsalDto>> UpdateAsync(
        Guid id, UpdateRehearsalRequest request, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetWithDetailsForUpdateAsync(id, cancellationToken);
        if (rehearsal is null)
        {
            return Result<RehearsalDto>.Failure(ErrorCodes.RehearsalNotFound);
        }

        if (rehearsal.EndTime < DateTime.UtcNow)
        {
            return Result<RehearsalDto>.Failure(ErrorCodes.RehearsalAlreadyPassed);
        }

        if (await CheckEventAsync(request.EventId, cancellationToken) is { } eventError)
        {
            return Result<RehearsalDto>.Failure(eventError);
        }

        var (location, locationError) = await CheckLocationAsync(
            request.LocationId, rehearsal.LocationId, request.StartTime, request.EndTime, rehearsal.Id, cancellationToken);
        if (locationError is not null)
        {
            return Result<RehearsalDto>.Failure(locationError);
        }

        rehearsal.EventId = request.EventId;
        rehearsal.LocationId = request.LocationId;
        rehearsal.Location = location;
        rehearsal.StartTime = request.StartTime;
        rehearsal.EndTime = request.EndTime;
        rehearsal.Note = request.Note;
        await rehearsalRepository.SaveChangesAsync(cancellationToken);

        await NotifyMembersAsync(
            "Buổi tập đã thay đổi",
            $"Buổi tập đã được cập nhật, bắt đầu lúc {FormatTime(rehearsal.StartTime)}"
                + (location is null ? "." : $" tại {location.Name}."),
            nameof(Rehearsal),
            rehearsal.Id,
            cancellationToken);

        return Result<RehearsalDto>.Success(mapper.Map<RehearsalDto>(rehearsal));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetWithAttendancesForUpdateAsync(id, cancellationToken);
        if (rehearsal is null)
        {
            return Result.Failure(ErrorCodes.RehearsalNotFound);
        }

        // Rows cascade with the rehearsal, so a recorded attendance would be lost silently.
        if (rehearsal.Attendances.Count > 0)
        {
            return Result.Failure(ErrorCodes.RehearsalHasAttendance);
        }

        rehearsalRepository.Remove(rehearsal);
        await rehearsalRepository.SaveChangesAsync(cancellationToken);

        // Nobody needs to hear that a rehearsal already over was removed.
        if (rehearsal.EndTime >= DateTime.UtcNow)
        {
            await NotifyMembersAsync(
                "Buổi tập đã bị huỷ",
                $"Buổi tập lúc {FormatTime(rehearsal.StartTime)} đã bị huỷ.",
                rehearsal.EventId is null ? null : nameof(LiturgicalEvent),
                rehearsal.EventId,
                cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>
    /// Loads the location and checks it can host [start, end). An inactive location is accepted only when it is
    /// <paramref name="currentLocationId"/>, so an edit is not blocked by a location retired after it was chosen.
    /// </summary>
    private async Task<(WorshipLocation? Location, string? Error)> CheckLocationAsync(
        Guid? locationId, Guid? currentLocationId, DateTime start, DateTime end, Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (locationId is null)
        {
            return (null, null);
        }

        var location = await locationRepository.GetByIdAsync(locationId.Value, cancellationToken);
        if (location is null)
        {
            return (null, ErrorCodes.LookupNotFound);
        }

        if (!location.IsActive && location.Id != currentLocationId)
        {
            return (null, ErrorCodes.LookupInactive);
        }

        return await rehearsalRepository.HasConflictAsync(location.Id, start, end, excludeId, cancellationToken)
            ? (null, ErrorCodes.RehearsalTimeConflict)
            : (location, null);
    }

    private async Task NotifyMembersAsync(
        string title, string content, string? referenceType, Guid? referenceId, CancellationToken cancellationToken)
    {
        var recipientUserIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ChoirMember], cancellationToken);

        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.RehearsalChanged, title, content, recipientUserIds, referenceType, referenceId),
            cancellationToken);
    }

    private static string FormatTime(DateTime utc) =>
        string.Create(CultureInfo.InvariantCulture, $"{VietnamTime.FromUtc(utc):HH:mm dd/MM/yyyy}");

    /// <summary>Error code when the linked event is missing or cancelled; null when there is no event or it is usable.</summary>
    private async Task<string?> CheckEventAsync(Guid? eventId, CancellationToken cancellationToken)
    {
        if (eventId is null)
        {
            return null;
        }

        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId.Value, cancellationToken);
        if (liturgicalEvent is null)
        {
            return ErrorCodes.LiturgicalEventNotFound;
        }

        return liturgicalEvent.Status == EventStatus.Cancelled ? ErrorCodes.LiturgicalEventCancelled : null;
    }
}
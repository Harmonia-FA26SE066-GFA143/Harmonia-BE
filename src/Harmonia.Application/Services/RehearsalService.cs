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

public class RehearsalService(
    IRehearsalRepository rehearsalRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IGenericRepository<WorshipLocation> locationRepository,
    IGenericRepository<Song> songRepository,
    IMapper mapper) : IRehearsalService
{
    public async Task<Result<RehearsalDto>> CreateAsync(
        CreateRehearsalRequest request, CancellationToken cancellationToken)
    {
        if (await CheckEventAsync(request.EventId, cancellationToken) is { } eventError)
        {
            return Result<RehearsalDto>.Failure(eventError);
        }

        WorshipLocation? location = null;
        if (request.LocationId is { } locationId)
        {
            location = await locationRepository.GetByIdAsync(locationId, cancellationToken);
            if (location is null)
            {
                return Result<RehearsalDto>.Failure(ErrorCodes.LookupNotFound);
            }

            if (!location.IsActive)
            {
                return Result<RehearsalDto>.Failure(ErrorCodes.LookupInactive);
            }

            if (await rehearsalRepository.HasConflictAsync(
                    locationId, request.StartTime, request.EndTime, cancellationToken))
            {
                return Result<RehearsalDto>.Failure(ErrorCodes.RehearsalTimeConflict);
            }
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

        var songsById = songs.ToDictionary(song => song.Id);
        var rehearsalSongs = request.Songs
            .Select((item, index) => new RehearsalSong
            {
                Id = Guid.NewGuid(),
                SongId = item.SongId,
                Song = songsById[item.SongId],
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

        return Result<RehearsalDto>.Success(mapper.Map<RehearsalDto>(rehearsal));
    }

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
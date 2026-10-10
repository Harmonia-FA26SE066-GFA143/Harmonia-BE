using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class RehearsalSongService(
    IRehearsalRepository rehearsalRepository,
    IGenericRepository<Song> songRepository,
    IMapper mapper) : IRehearsalSongService
{
    public async Task<Result<List<RehearsalSongDto>>> GetAsync(Guid rehearsalId, CancellationToken cancellationToken)
    {
        if (await rehearsalRepository.GetByIdAsync(rehearsalId, cancellationToken) is null)
        {
            return Result<List<RehearsalSongDto>>.Failure(ErrorCodes.RehearsalNotFound);
        }

        var songs = await rehearsalRepository.GetSongsAsync(rehearsalId, cancellationToken);

        return Result<List<RehearsalSongDto>>.Success(mapper.Map<List<RehearsalSongDto>>(songs));
    }

    public async Task<Result<List<RehearsalSongDto>>> UpdateAsync(
        Guid rehearsalId, UpdateRehearsalSongsRequest request, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetWithSongsForUpdateAsync(rehearsalId, cancellationToken);
        if (rehearsal is null) return Result<List<RehearsalSongDto>>.Failure(ErrorCodes.RehearsalNotFound);

        var songIds = request.Items.Select(i => i.SongId).ToList();
        var songs = await songRepository.ListAsync(x => songIds.Contains(x.Id), cancellationToken);
        if (songs.Count != songIds.Count) return Result<List<RehearsalSongDto>>.Failure(ErrorCodes.SongNotFound);

        // A song retired after it was put on the programme may stay; only adding an inactive one is refused.
        var existingSongIds = rehearsal.Songs.Select(s => s.SongId).ToHashSet();
        if (songs.Any(s => !s.IsActive && !existingSongIds.Contains(s.Id)))
        {
            return Result<List<RehearsalSongDto>>.Failure(ErrorCodes.SongInactive);
        }

        foreach (var removed in rehearsal.Songs.Where(s => !songIds.Contains(s.SongId)).ToList())
        {
            rehearsal.Songs.Remove(removed);
        }

        // Kept rows are updated in place, so the unique (RehearsalId, SongId) index never sees a delete-then-insert.
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var rehearsalSong = rehearsal.Songs.FirstOrDefault(s => s.SongId == item.SongId);
            if (rehearsalSong is null)
            {
                // Id left unset: a preset key on a row reached through a tracked navigation reads to EF as an update.
                rehearsalSong = new RehearsalSong { RehearsalId = rehearsal.Id, SongId = item.SongId };
                rehearsal.Songs.Add(rehearsalSong);
            }

            rehearsalSong.DisplayOrder = index + 1;
            rehearsalSong.Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();
        }

        await rehearsalRepository.SaveChangesAsync(cancellationToken);

        var saved = await rehearsalRepository.GetSongsAsync(rehearsalId, cancellationToken);
        return Result<List<RehearsalSongDto>>.Success(mapper.Map<List<RehearsalSongDto>>(saved));
    }
}

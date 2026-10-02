using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class SongService(ISongRepository songRepository, IMapper mapper) : ISongService
{
    public async Task<Result<PagedList<SongDto>>> SearchAsync(SearchSongsRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await songRepository.SearchAsync(keyword, request, cancellationToken);

        return Result<PagedList<SongDto>>.Success(new PagedList<SongDto>(
            mapper.Map<List<SongDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<SongDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);

        return song is null
            ? Result<SongDto>.Failure(ErrorCodes.SongNotFound)
            : Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result<SongDto>> CreateAsync(CreateSongRequest request, CancellationToken cancellationToken)
    {
        var song = mapper.Map<Song>(request);
        song.Id = Guid.NewGuid();

        if (await songRepository.ExistsByTitleAsync(song.Title, song.Composer, null, cancellationToken))
            return Result<SongDto>.Failure(ErrorCodes.SongTitleDuplicate);

        await songRepository.AddAsync(song, cancellationToken);
        await songRepository.SaveChangesAsync(cancellationToken);
        return Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result<SongDto>> UpdateAsync(Guid id, UpdateSongRequest request, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);
        if (song is null) return Result<SongDto>.Failure(ErrorCodes.SongNotFound);

        // Mapped onto the tracked entity; on a duplicate nothing is saved, so the change is discarded with the request.
        mapper.Map(request, song);

        if (await songRepository.ExistsByTitleAsync(song.Title, song.Composer, id, cancellationToken))
            return Result<SongDto>.Failure(ErrorCodes.SongTitleDuplicate);

        await songRepository.SaveChangesAsync(cancellationToken);
        return Result<SongDto>.Success(mapper.Map<SongDto>(song));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await GetActiveAsync(id, cancellationToken);
        if (song is null) return Result.Failure(ErrorCodes.SongNotFound);

        song.IsActive = false;
        await songRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Song?> GetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var song = await songRepository.GetByIdAsync(id, cancellationToken);
        return song is { IsActive: true } ? song : null;
    }
}

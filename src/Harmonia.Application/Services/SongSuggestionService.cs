using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Services;

public class SongSuggestionService(
    ILiturgicalEventRepository liturgicalEventRepository,
    ISongRepository songRepository,
    IChoirScopeService choirScope) : ISongSuggestionService
{
    private const int SlotScore = 3, EventTypeScore = 2, SeasonScore = 2;

    public async Task<Result<PagedList<SongSuggestionResponse>>> GetForEventAsync(
        Guid eventId, SearchSongSuggestionsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (liturgicalEvent is null || !await choirScope.CanViewEventAsync(eventId, cancellationToken))
            return Result<PagedList<SongSuggestionResponse>>.Failure(ErrorCodes.LiturgicalEventNotFound);

        var matches = await songRepository.GetSuggestionMatchesAsync(
            liturgicalEvent.LiturgicalSeasonId, liturgicalEvent.MassTypeId, liturgicalEvent.CeremonyTypeId, request.SlotId, cancellationToken);

        // shortcut: ranks and pages every match in memory; fine for a parish library, move to SQL past a few thousand songs.
        var stats = matches.Count == 0
            ? []
            : await songRepository.GetStatsAsync(matches.Select(x => x.Song.Id).ToList(), VietnamTime.Today, cancellationToken);

        // Dimensions the event sets but the song leaves blank: a blank fits anything, so it says less than a real match.
        var hasEventType = liturgicalEvent.MassTypeId is not null || liturgicalEvent.CeremonyTypeId is not null;
        int Unclassified((Domain.Entities.Song Song, bool MatchesSeason, bool MatchesEventType, bool MatchesSlot, bool HasSeason, bool HasEventType, bool HasSlot) x) =>
            (liturgicalEvent.LiturgicalSeasonId is not null && !x.HasSeason ? 1 : 0)
            + (hasEventType && !x.HasEventType ? 1 : 0)
            + (request.SlotId is not null && !x.HasSlot ? 1 : 0);

        var ranked = matches
            .Select(x => (Unclassified: Unclassified(x), Dto: new SongSuggestionResponse
            {
                SongId = x.Song.Id,
                Title = x.Song.Title,
                Composer = x.Song.Composer,
                Score = (x.MatchesSlot ? SlotScore : 0) + (x.MatchesEventType ? EventTypeScore : 0) + (x.MatchesSeason ? SeasonScore : 0),
                MatchesSlot = x.MatchesSlot,
                MatchesEventType = x.MatchesEventType,
                MatchesSeason = x.MatchesSeason,
                TimesUsed = stats.GetValueOrDefault(x.Song.Id)?.TimesUsed ?? 0,
                LastUsedDate = stats.GetValueOrDefault(x.Song.Id)?.LastUsedDate,
            }))
            .OrderByDescending(x => x.Dto.Score)
            .ThenBy(x => x.Unclassified)
            .ThenBy(x => x.Dto.LastUsedDate ?? DateOnly.MinValue)
            .ThenBy(x => x.Dto.Title)
            .Select(x => x.Dto)
            .ToList();

        var page = ranked.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToList();
        return Result<PagedList<SongSuggestionResponse>>.Success(
            new PagedList<SongSuggestionResponse>(page, request.PageNumber, request.PageSize, ranked.Count));
    }
}

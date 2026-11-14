using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Songs that fit an event, from the library's classification, to help build its song list.</summary>
public interface ISongSuggestionService
{
    /// <summary>
    /// Active songs matching the event's season, Mass or ceremony type, or the given slot, best score first, then the songs
    /// leaving fewer of those dimensions unclassified, then the longest unsung (never sung first), then title. Songs classified only under other seasons or other slots are
    /// left out. An event outside the caller's scope is LITURGICAL_EVENT_NOT_FOUND.
    /// </summary>
    Task<Result<PagedList<SongSuggestionResponse>>> GetForEventAsync(
        Guid eventId, SearchSongSuggestionsRequest request, CancellationToken cancellationToken);
}

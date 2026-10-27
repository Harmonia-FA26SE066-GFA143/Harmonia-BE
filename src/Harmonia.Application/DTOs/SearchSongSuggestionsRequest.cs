using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/liturgical-events/{id}/song-suggestions; SlotId narrows to one part of the Mass.</summary>
public class SearchSongSuggestionsRequest : PagingRequest
{
    public Guid? SlotId { get; set; }
}

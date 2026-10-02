using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>Query string of GET api/songs: paging plus a free-text keyword over title, composer and lyricist.</summary>
public class SearchSongsRequest : PagingRequest
{
    public string? Keyword { get; set; }
}

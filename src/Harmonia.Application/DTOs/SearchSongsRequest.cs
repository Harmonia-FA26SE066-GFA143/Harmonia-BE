using Harmonia.Application.Common.Models;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/songs: paging, a free-text keyword over title, composer and lyricist,
/// and optional classification filters (UC-21 / FE-29). Filters combine with AND.
/// </summary>
public class SearchSongsRequest : PagingRequest
{
    public string? Keyword { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    public Guid? MassTypeId { get; set; }

    public Guid? CeremonyTypeId { get; set; }

    public Guid? SongThemeId { get; set; }

    /// <summary>Matches a vocal or an instrument requirement.</summary>
    public Guid? SkillId { get; set; }
}

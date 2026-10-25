using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/songs: paging, a free-text keyword over title, composer and lyricist (case and
/// Vietnamese diacritics ignored), and optional classification and library filters (UC-21 / FE-29).
/// Filters combine with AND.
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

    /// <summary>False keeps only songs with no season, Mass type, ceremony type or theme yet; true the others.</summary>
    public bool? IsClassified { get; set; }

    /// <summary>Keeps only songs with no active material of this type, e.g. SampleAudio.</summary>
    public MaterialType? MissingMaterialType { get; set; }
}

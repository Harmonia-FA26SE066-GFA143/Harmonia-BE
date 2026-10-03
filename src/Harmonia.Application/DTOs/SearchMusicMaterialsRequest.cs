using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/music-materials/mine: paging plus optional filters over the calling
/// member's materials (UC-07E). Filters combine with AND.
/// </summary>
public class SearchMusicMaterialsRequest : PagingRequest
{
    /// <summary>Matches the song title or the material title.</summary>
    public string? Keyword { get; set; }

    public Guid? SongId { get; set; }

    public Guid? LiturgicalSeasonId { get; set; }

    /// <summary>Vocal part or instrument; matches only materials targeted at that skill, not those for everyone.</summary>
    public Guid? SkillId { get; set; }

    public MaterialType? MaterialType { get; set; }
}

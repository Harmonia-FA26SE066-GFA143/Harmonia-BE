using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>Row of GET api/liturgical-events: the event plus the catalog names and song list state a list page shows.</summary>
public class LiturgicalEventListItemDto : LiturgicalEventDto
{
    public string? LiturgicalSeasonName { get; set; }

    public string? MassTypeName { get; set; }

    public string? CeremonyTypeName { get; set; }

    /// <summary>Status of the newest song list version; null when the event has none yet.</summary>
    public SongListStatus? LatestSongListStatus { get; set; }

    /// <summary>EventDate minus the SongList.LeadDays setting; a reminder for the Choir Director, not enforced.</summary>
    public DateOnly SongListDueDate { get; set; }
}

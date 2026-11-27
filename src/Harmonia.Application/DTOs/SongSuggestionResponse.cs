namespace Harmonia.Application.DTOs;

/// <summary>A song proposed for an event, with why it fits and how recently it was sung, best first.</summary>
public class SongSuggestionResponse
{
    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Composer { get; set; }

    /// <summary>3 for the slot, 2 for the Mass or ceremony type, 2 for the season.</summary>
    public int Score { get; set; }

    public bool MatchesSlot { get; set; }

    /// <summary>Classified under the event's Mass type or ceremony type.</summary>
    public bool MatchesEventType { get; set; }

    public bool MatchesSeason { get; set; }

    public int TimesUsed { get; set; }

    public DateOnly? LastUsedDate { get; set; }
}

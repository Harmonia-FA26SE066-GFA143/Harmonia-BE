namespace Harmonia.Application.DTOs;

/// <summary>Result of a calendar sync: how many days were stored and which upcoming events sit on a day that changed.</summary>
public class SyncLiturgicalDaysResponse
{
    public int SyncedCount { get; set; }

    public List<SyncAffectedEventDto> AffectedEvents { get; set; } = [];
}

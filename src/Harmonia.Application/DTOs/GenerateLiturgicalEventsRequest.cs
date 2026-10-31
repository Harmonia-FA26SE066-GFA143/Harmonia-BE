namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/liturgical-events/generation: both dates inclusive, at most 62 days, not in the past.</summary>
public class GenerateLiturgicalEventsRequest
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }
}

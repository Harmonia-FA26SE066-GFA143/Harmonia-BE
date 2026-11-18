namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/liturgical-events/generation: both dates inclusive, at most 62 days, not in the past.</summary>
public class GenerateLiturgicalEventsRequest
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>Choirs serving every created draft; empty leaves the drafts without a choir, as before (B55).</summary>
    public List<Guid> ChoirIds { get; set; } = [];

    /// <summary>Required among <see cref="ChoirIds"/> when choirs are given.</summary>
    public Guid? LeadChoirId { get; set; }
}

using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class RosterSuggestionResponse
{
    public Guid RosterId { get; set; }

    public Guid EventId { get; set; }

    public RosterStatus Status { get; set; }

    public DateTime? GeneratedAt { get; set; }

    /// <summary>False when the AI failed or had nothing to pick, so the rule-based fallback produced the whole suggestion.</summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>Every assignment of the roster: the new suggestions plus the manual ones that were kept.</summary>
    public List<RosterAssignmentDto> Assignments { get; set; } = [];

    public List<RosterShortageDto> Shortages { get; set; } = [];
}

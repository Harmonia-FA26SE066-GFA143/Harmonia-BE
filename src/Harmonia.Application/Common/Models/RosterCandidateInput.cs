namespace Harmonia.Application.Common.Models;

/// <summary>A member eligible for one slot. Same member keeps the same code across slots.</summary>
public class RosterCandidateInput
{
    public string MemberCode { get; set; } = string.Empty;

    /// <summary>Events served in the last 60 days, so the generator can spread the load.</summary>
    public int RecentServiceCount { get; set; }
}

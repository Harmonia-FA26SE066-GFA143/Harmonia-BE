namespace Harmonia.Application.Common.Models;

/// <summary>
/// One song / skill pair, or one whole-service skill, still needing people, as sent to the roster suggestion generator.
/// Anonymised: codes like "S1" / "M1" stand in for ids, and no personal data is included.
/// </summary>
public class RosterSlotInput
{
    public string SlotCode { get; set; } = string.Empty;

    public string SkillName { get; set; } = string.Empty;

    public int NeededCount { get; set; }

    /// <summary>True when the member serves the whole event (organist, conductor), not one song.</summary>
    public bool IsWholeService { get; set; }

    public List<RosterCandidateInput> Candidates { get; set; } = [];
}

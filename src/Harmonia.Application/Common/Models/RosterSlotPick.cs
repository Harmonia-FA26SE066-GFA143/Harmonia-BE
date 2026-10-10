namespace Harmonia.Application.Common.Models;

/// <summary>Members the generator picked for one slot. Untrusted: RosterService re-checks every code.</summary>
public class RosterSlotPick
{
    public string SlotCode { get; set; } = string.Empty;

    public List<string> MemberCodes { get; set; } = [];
}

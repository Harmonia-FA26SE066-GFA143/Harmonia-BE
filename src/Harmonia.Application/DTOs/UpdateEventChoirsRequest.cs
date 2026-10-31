namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/liturgical-events/{id}/choirs. Replaces the whole set of choirs serving the event;
/// an empty list removes every choir.
/// </summary>
public class UpdateEventChoirsRequest
{
    public List<Guid> ChoirIds { get; set; } = [];

    /// <summary>The lead choir (D8): required when <see cref="ChoirIds"/> is not empty, and must be one of them.</summary>
    public Guid? LeadChoirId { get; set; }
}

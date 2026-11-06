namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/liturgical-events/{id}/choirs. Replaces the whole set of choirs serving the event;
/// the list cannot be empty.
/// </summary>
public class UpdateEventChoirsRequest
{
    public List<Guid> ChoirIds { get; set; } = [];

    /// <summary>The lead choir (D8): required, and must be one of <see cref="ChoirIds"/>.</summary>
    public Guid? LeadChoirId { get; set; }
}

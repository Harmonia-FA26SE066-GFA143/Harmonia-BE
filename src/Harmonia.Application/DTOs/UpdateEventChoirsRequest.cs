namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/liturgical-events/{id}/choirs. Replaces the whole set of choirs serving the event;
/// an empty list removes every choir.
/// </summary>
public class UpdateEventChoirsRequest
{
    public List<Guid> ChoirIds { get; set; } = [];
}

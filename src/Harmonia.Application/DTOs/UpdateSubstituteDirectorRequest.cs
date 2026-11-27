namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/liturgical-events/{id}/choirs/{choirId}/substitute-director. <see cref="DirectorId"/> null clears the
/// substitute and hands the event back to the choir's own director.
/// </summary>
public class UpdateSubstituteDirectorRequest
{
    public Guid? DirectorId { get; set; }

    /// <summary>Why the substitute is needed, e.g. the director is suddenly absent. Required when setting one.</summary>
    public string? Reason { get; set; }
}

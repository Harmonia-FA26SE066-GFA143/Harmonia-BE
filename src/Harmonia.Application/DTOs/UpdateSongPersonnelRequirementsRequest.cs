namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/song-list-items/{id}/personnel-requirements: the full desired set, replacing
/// whatever the song list item had. A skill left out is removed.
/// </summary>
public class UpdateSongPersonnelRequirementsRequest
{
    public List<UpdateSongPersonnelRequirementRequest> Requirements { get; set; } = [];
}

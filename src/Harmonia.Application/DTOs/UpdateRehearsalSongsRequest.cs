namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/rehearsals/{id}/songs: the full programme, replacing what was there.
/// The position in <see cref="Items"/> is the display order; an empty list clears the programme.
/// </summary>
public class UpdateRehearsalSongsRequest
{
    public List<UpdateRehearsalSongRequest> Items { get; set; } = [];
}

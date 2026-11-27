namespace Harmonia.Application.DTOs;

public class UpdateRehearsalSongRequest
{
    public Guid SongId { get; set; }

    public string? Note { get; set; }
}

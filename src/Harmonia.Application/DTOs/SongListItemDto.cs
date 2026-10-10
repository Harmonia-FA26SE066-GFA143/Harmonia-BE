namespace Harmonia.Application.DTOs;

public class SongListItemDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public string SongTitle { get; set; } = string.Empty;

    public Guid SlotId { get; set; }

    public string SlotName { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }
}
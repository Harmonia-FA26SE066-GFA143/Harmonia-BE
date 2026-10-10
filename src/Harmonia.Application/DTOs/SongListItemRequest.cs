using System;

namespace Harmonia.Application.DTOs;

public class SongListItemRequest
{
    public Guid SongId { get; set; }

    public Guid SlotId { get; set; }

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }
}
using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class CreateSongListRequest
{
    public Guid EventId { get; set; }

    public List<SongListItemRequest> Items { get; set; } = [];
}
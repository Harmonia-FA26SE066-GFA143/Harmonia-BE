using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class UpdateSongListItemsRequest
{
    public List<SongListItemRequest> Items { get; set; } = [];
}
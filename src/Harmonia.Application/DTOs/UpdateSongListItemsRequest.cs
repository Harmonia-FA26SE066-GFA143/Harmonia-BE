using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class UpdateSongListItemsRequest
{
    public List<UpdateSongListItemRequest> Items { get; set; } = [];
}
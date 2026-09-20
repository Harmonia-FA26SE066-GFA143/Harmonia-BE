using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class LiturgicalSlot : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int DefaultOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SongListItem> SongListItems { get; set; } = [];
}

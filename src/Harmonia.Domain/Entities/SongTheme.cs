using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongTheme : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Themes form a tree of any depth (Feasts of the Lord > Mary > Rosary); null is a root.
    public Guid? ParentId { get; set; }

    public SongTheme? Parent { get; set; }

    public ICollection<SongTheme> Children { get; set; } = [];
}

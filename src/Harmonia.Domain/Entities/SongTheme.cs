using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class SongTheme : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

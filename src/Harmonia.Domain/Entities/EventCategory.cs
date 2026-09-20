using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class EventCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LiturgicalEvent> LiturgicalEvents { get; set; } = [];
}

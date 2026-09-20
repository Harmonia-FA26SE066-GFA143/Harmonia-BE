using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class WorshipLocation : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LiturgicalEvent> LiturgicalEvents { get; set; } = [];

    public ICollection<Rehearsal> Rehearsals { get; set; } = [];
}

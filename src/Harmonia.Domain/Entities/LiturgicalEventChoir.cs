using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class LiturgicalEventChoir : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid ChoirId { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public Choir Choir { get; set; } = null!;
}

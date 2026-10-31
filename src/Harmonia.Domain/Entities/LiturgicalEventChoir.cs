using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class LiturgicalEventChoir : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid ChoirId { get; set; }

    /// <summary>
    /// The lead choir plans the event: its director builds the song list, the roster and the invitations (D8).
    /// At most one per event, chosen by the Parish Priest.
    /// </summary>
    public bool IsLead { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public Choir Choir { get; set; } = null!;
}

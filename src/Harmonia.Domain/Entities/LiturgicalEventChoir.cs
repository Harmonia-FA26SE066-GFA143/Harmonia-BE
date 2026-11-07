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

    /// <summary>
    /// Another Choir Director standing in for this choir's own director at this event only, e.g. when the director is
    /// suddenly absent. Chosen by the Parish Priest; they get the director's rights on this event alone.
    /// </summary>
    public Guid? SubstituteDirectorId { get; set; }

    /// <summary>Why the Parish Priest last changed this row: the substitute director, or the choir taking over.</summary>
    public string? Reason { get; set; }

    public LiturgicalEvent LiturgicalEvent { get; set; } = null!;

    public Choir Choir { get; set; } = null!;

    public User? SubstituteDirector { get; set; }
}

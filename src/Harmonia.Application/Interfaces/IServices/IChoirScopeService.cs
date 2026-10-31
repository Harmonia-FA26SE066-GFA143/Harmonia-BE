namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Which choirs and events the caller may manage (D8). Only a Choir Director is limited; every other role passes,
/// since the controller's role attribute already decides what they may call. A record that has no choir yet
/// (data from before D8, or an event no choir was assigned to) is open to every director.
/// </summary>
public interface IChoirScopeService
{
    /// <summary>Choirs the calling director leads; null when the caller is not a Choir Director, meaning no filter.</summary>
    Task<IReadOnlyCollection<Guid>?> GetDirectedChoirIdsAsync(CancellationToken cancellationToken);

    /// <summary>True for a record with no choir, for a choir the calling director leads, and for any other role.</summary>
    Task<bool> CanManageChoirAsync(Guid? choirId, CancellationToken cancellationToken);

    /// <summary>
    /// Error code when a new rehearsal or practice assignment cannot go to the choir, null when it can: the choir must
    /// exist and be led by the caller (CHOIR_NOT_FOUND), be active (CHOIR_INACTIVE), and serve the linked event if
    /// that event has choirs (CHOIR_NOT_SERVING_EVENT).
    /// </summary>
    Task<string?> CheckChoirForNewRecordAsync(Guid choirId, Guid? eventId, CancellationToken cancellationToken);

    /// <summary>True when no choir serves the event or one of the caller's choirs does.</summary>
    Task<bool> CanViewEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// True when the caller may plan the event: no choir serves it, or the caller leads its lead choir. An event whose
    /// choirs were assigned without a lead lets the director of any serving choir plan it.
    /// </summary>
    Task<bool> CanLeadEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Null when the caller may plan the event (<see cref="CanLeadEventAsync"/>); <paramref name="notFoundCode"/> when
    /// they cannot even see it, so it stays hidden; CHOIR_NOT_LEAD when one of their choirs serves it but does not lead.
    /// </summary>
    Task<string?> CheckCanLeadEventAsync(Guid eventId, string notFoundCode, CancellationToken cancellationToken);

    /// <summary>The event's lead choir, else its only choir, else null.</summary>
    Task<Guid?> GetLeadChoirIdAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Member profile ids active in the choirs serving the event; null when no choir serves it.</summary>
    Task<IReadOnlyCollection<Guid>?> GetEventMemberIdsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Member profile ids active in the choir; null when <paramref name="choirId"/> is null.</summary>
    Task<IReadOnlyCollection<Guid>?> GetChoirMemberIdsAsync(Guid? choirId, CancellationToken cancellationToken);

    /// <summary>Choirs the member profile is active in.</summary>
    Task<IReadOnlyCollection<Guid>> GetMemberChoirIdsAsync(Guid memberId, CancellationToken cancellationToken);
}

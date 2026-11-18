using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

/// <summary>
/// Rehearsal scope built on <see cref="IChoirScopeService"/>: a joint rehearsal follows its event's choirs, any other
/// follows its own ChoirId. Kept as extensions over the interface so every caller and test fake shares one rule.
/// A joint rehearsal whose event was deleted (EventId set null) falls back to the no-choir rule.
/// </summary>
public static class ChoirScopeServiceExtensions
{
    /// <summary>
    /// Null when the caller may change the rehearsal; REHEARSAL_NOT_FOUND when it is outside their scope; for a joint
    /// rehearsal, CHOIR_NOT_LEAD when their choir serves the event but does not lead it.
    /// </summary>
    public static async Task<string?> CheckCanManageRehearsalAsync(
        this IChoirScopeService scope, Rehearsal rehearsal, CancellationToken cancellationToken)
    {
        if (rehearsal is { IsJoint: true, EventId: { } eventId })
        {
            return await scope.CheckCanLeadEventAsync(eventId, ErrorCodes.RehearsalNotFound, cancellationToken);
        }

        return await scope.CanManageChoirAsync(rehearsal.ChoirId, cancellationToken) ? null : ErrorCodes.RehearsalNotFound;
    }

    /// <summary>True when the caller may read the rehearsal: for a joint one, any director of a serving choir.</summary>
    public static Task<bool> CanViewRehearsalAsync(
        this IChoirScopeService scope, Rehearsal rehearsal, CancellationToken cancellationToken) =>
        rehearsal is { IsJoint: true, EventId: { } eventId }
            ? scope.CanViewEventAsync(eventId, cancellationToken)
            : scope.CanManageChoirAsync(rehearsal.ChoirId, cancellationToken);

    /// <summary>Member profile ids expected at the rehearsal; null means every member (no choir to narrow it).</summary>
    public static Task<IReadOnlyCollection<Guid>?> GetRehearsalMemberIdsAsync(
        this IChoirScopeService scope, Rehearsal rehearsal, CancellationToken cancellationToken) =>
        rehearsal is { IsJoint: true, EventId: { } eventId }
            ? scope.GetEventMemberIdsAsync(eventId, cancellationToken)
            : scope.GetChoirMemberIdsAsync(rehearsal.ChoirId, cancellationToken);
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IChoirJoinRequestRepository : IGenericRepository<ChoirJoinRequest>
{
    /// <summary>
    /// Read-only, with the member's user, the member's active choir memberships (with their choir) and the choir
    /// loaded.
    /// </summary>
    Task<ChoirJoinRequest?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Newest first, with the same details as <see cref="GetWithDetailsAsync"/>. The scope filters combine with AND:
    /// <paramref name="memberId"/> keeps that member's rows, <paramref name="directorUserId"/> keeps rows of the choirs
    /// that user directs.
    /// </summary>
    Task<PagedList<ChoirJoinRequest>> SearchAsync(
        SearchChoirJoinRequestsRequest filter, Guid? memberId, Guid? directorUserId, CancellationToken cancellationToken);

    /// <summary>Tracked Pending rows of the members, in both directions.</summary>
    Task<List<ChoirJoinRequest>> GetPendingForUpdateAsync(
        IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken);
}

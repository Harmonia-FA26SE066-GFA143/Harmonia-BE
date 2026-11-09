using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IDirectorNominationRepository : IGenericRepository<DirectorNomination>
{
    /// <summary>
    /// Read-only, with the member's user, the nominator and both choirs loaded. The visibility filters combine with
    /// AND: <paramref name="nominatedById"/> keeps the caller's own nominations, <paramref name="memberUserId"/> keeps
    /// nominations of that member that the Parish Priest has approved at some point.
    /// </summary>
    Task<DirectorNomination?> GetWithDetailsAsync(
        Guid id, Guid? nominatedById, Guid? memberUserId, CancellationToken cancellationToken);

    /// <summary>Newest first, with the same details and visibility filters as <see cref="GetWithDetailsAsync"/>.</summary>
    Task<PagedList<DirectorNomination>> SearchAsync(
        SearchDirectorNominationsRequest filter, Guid? nominatedById, Guid? memberUserId,
        CancellationToken cancellationToken);

    /// <summary>True when the member has a nomination that is not yet appointed, rejected or cancelled.</summary>
    Task<bool> HasOpenAsync(Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Track record of a member: rehearsal attendance counts from <paramref name="attendanceSinceUtc"/>, and events
    /// served up to <paramref name="today"/>.
    /// </summary>
    Task<MemberServiceStats> GetMemberStatsAsync(
        Guid memberId, DateTime attendanceSinceUtc, DateOnly today, CancellationToken cancellationToken);
}

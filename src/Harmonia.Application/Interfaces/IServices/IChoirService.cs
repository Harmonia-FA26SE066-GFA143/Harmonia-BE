using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>
/// Choirs of the parish, their members and the events they serve (D7). The Admin creates choirs and assigns each one
/// Choir Director; a director manages only their own choirs, and another director's choir reads as not found.
/// </summary>
public interface IChoirService
{
    Task<Result<ChoirDto>> CreateAsync(CreateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>Every choir for the Admin, only the caller's own choirs for a Choir Director; ordered by name.</summary>
    Task<Result<List<ChoirDto>>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<ChoirDto>> UpdateAsync(Guid id, UpdateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>Active members of the choir, ordered by name.</summary>
    Task<Result<List<ChoirMemberDto>>> GetMembersAsync(Guid choirId, CancellationToken cancellationToken);

    Task<Result> AddMembersAsync(Guid choirId, AddChoirMembersRequest request, CancellationToken cancellationToken);

    /// <summary>Marks the membership as left; the row stays as history.</summary>
    Task<Result> RemoveMemberAsync(Guid choirId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>Choirs serving the event, the lead choir first, then by name.</summary>
    Task<Result<List<LiturgicalEventChoirDto>>> GetEventChoirsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// The Parish Priest replaces the choirs serving the event and picks the lead choir (D8). Cancelled and past
    /// events are locked.
    /// </summary>
    Task<Result<List<LiturgicalEventChoirDto>>> UpdateEventChoirsAsync(
        Guid eventId, UpdateEventChoirsRequest request, CancellationToken cancellationToken);
}

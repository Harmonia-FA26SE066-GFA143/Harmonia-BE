using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>Choirs of the parish, their members and the events they serve (D7). The Choir Director manages every choir.</summary>
public interface IChoirService
{
    Task<Result<ChoirDto>> CreateAsync(CreateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>Every choir, ordered by name.</summary>
    Task<Result<List<ChoirDto>>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<ChoirDto>> UpdateAsync(Guid id, UpdateChoirRequest request, CancellationToken cancellationToken);

    /// <summary>Active members of the choir, ordered by name.</summary>
    Task<Result<List<ChoirMemberDto>>> GetMembersAsync(Guid choirId, CancellationToken cancellationToken);

    Task<Result> AddMembersAsync(Guid choirId, AddChoirMembersRequest request, CancellationToken cancellationToken);

    /// <summary>Marks the membership as left; the row stays as history.</summary>
    Task<Result> RemoveMemberAsync(Guid choirId, Guid memberId, CancellationToken cancellationToken);

    /// <summary>Choirs serving the event, ordered by name.</summary>
    Task<Result<List<ChoirDto>>> GetEventChoirsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Replaces the choirs serving the event. Cancelled and past events are locked.</summary>
    Task<Result<List<ChoirDto>>> UpdateEventChoirsAsync(
        Guid eventId, UpdateEventChoirsRequest request, CancellationToken cancellationToken);
}

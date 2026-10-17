using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface ISongListService
{
    Task<Result<SongListDto>> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Newest version of the event's song list in any status (B14), so the director can reopen a draft.
    /// Draft versions are the director's own work: without <paramref name="includeDraft"/> the newest submitted one is returned.
    /// </summary>
    Task<Result<SongListDto>> GetLatestForEventAsync(Guid eventId, bool includeDraft, CancellationToken cancellationToken);

    /// <summary>Without <paramref name="includeDraft"/> a Draft reads as missing, as in <see cref="GetLatestForEventAsync"/>.</summary>
    Task<Result<SongListDto>> GetByIdAsync(Guid id, bool includeDraft, CancellationToken cancellationToken);

    Task<Result<List<SongListDto>>> GetPendingAsync(CancellationToken cancellationToken);

    Task<Result<SongListDto>> CreateAsync(CreateSongListRequest request, Guid proposedBy, CancellationToken cancellationToken);

    Task<Result<SongListDto>> UpdateItemsAsync(Guid id, UpdateSongListItemsRequest request, CancellationToken cancellationToken);

    Task<Result<SongListDto>> SubmitAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<SongListDto>> ReviewAsync(Guid id, ReviewSongListRequest request, Guid reviewerId, CancellationToken cancellationToken);
}
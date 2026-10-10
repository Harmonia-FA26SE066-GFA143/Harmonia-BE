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

    Task<Result<SongListDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<List<SongListDto>>> GetPendingAsync(CancellationToken cancellationToken);

    Task<Result<SongListDto>> CreateAsync(CreateSongListRequest request, Guid proposedBy, CancellationToken cancellationToken);

    Task<Result<SongListDto>> UpdateItemsAsync(Guid id, UpdateSongListItemsRequest request, CancellationToken cancellationToken);

    Task<Result<SongListDto>> SubmitAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<SongListDto>> ReviewAsync(Guid id, ReviewSongListRequest request, Guid reviewerId, CancellationToken cancellationToken);
}
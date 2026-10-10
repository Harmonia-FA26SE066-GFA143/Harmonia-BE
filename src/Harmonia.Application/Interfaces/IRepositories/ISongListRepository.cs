using Harmonia.Domain.Entities; 
using Harmonia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ISongListRepository : IGenericRepository<SongList>
{
    Task<SongList?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<SongList?> GetLatestVersionAsync(Guid eventId, CancellationToken cancellationToken);

    Task<SongList?> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken);

    Task<List<SongList>> GetByStatusAsync(SongListStatus status, CancellationToken cancellationToken);

    Task<List<SongListItem>> GetItemsAsync(Guid songListId, CancellationToken cancellationToken);

    Task RemoveItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken);

    Task AddItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken);

    Task AddReviewAsync(SongListReview review, CancellationToken cancellationToken);
}
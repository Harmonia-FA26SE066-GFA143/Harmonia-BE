using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Infrastructure.Repositories;

public class SongListRepository(HarmoniaDbContext dbContext)
    : GenericRepository<SongList>(dbContext), ISongListRepository
{
    public Task<SongList?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .Include(x => x.Items).ThenInclude(i => i.Song)
            .Include(x => x.Items).ThenInclude(i => i.Slot)
            .Include(x => x.Reviews)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<SongList?> GetLatestVersionAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SongList?> GetApprovedForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .Include(x => x.Items).ThenInclude(i => i.Song)
            .Include(x => x.Items).ThenInclude(i => i.Slot)
            .Include(x => x.Reviews)
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.Status == SongListStatus.Approved, cancellationToken);

    public Task<List<SongList>> GetByStatusAsync(SongListStatus status, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .Include(x => x.LiturgicalEvent)
            .Where(x => x.Status == status)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(cancellationToken);

    public Task<List<SongListItem>> GetItemsAsync(Guid songListId, CancellationToken cancellationToken) =>
        DbContext.SongListItems.Where(x => x.SongListId == songListId).ToListAsync(cancellationToken);

    public Task RemoveItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken)
    {
        DbContext.SongListItems.RemoveRange(items);
        return Task.CompletedTask;
    }

    public async Task AddItemsAsync(IEnumerable<SongListItem> items, CancellationToken cancellationToken) =>
        await DbContext.SongListItems.AddRangeAsync(items, cancellationToken);

    public async Task AddReviewAsync(SongListReview review, CancellationToken cancellationToken) =>
        await DbContext.SongListReviews.AddAsync(review, cancellationToken);
}
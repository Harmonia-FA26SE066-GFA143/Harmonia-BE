using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
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
            .AsSplitQuery()
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
            .AsSplitQuery()
            .Include(x => x.Items).ThenInclude(i => i.Song)
            .Include(x => x.Items).ThenInclude(i => i.Slot)
            .Include(x => x.Reviews)
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.Status == SongListStatus.Approved, cancellationToken);

    public Task<SongList?> GetLatestWithDetailsAsync(Guid eventId, bool includeDraft, CancellationToken cancellationToken) =>
        DbContext.SongLists
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Items).ThenInclude(i => i.Song)
            .Include(x => x.Items).ThenInclude(i => i.Slot)
            .Include(x => x.Reviews)
            .Where(x => x.EventId == eventId && (includeDraft || x.Status != SongListStatus.Draft))
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

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

    public Task<List<SongListItem>> GetItemsWithRequirementsAsync(Guid songListId, CancellationToken cancellationToken) =>
        DbContext.SongListItems
            .AsNoTracking()
            .Include(x => x.PersonnelRequirements)
            .Where(x => x.SongListId == songListId)
            .ToListAsync(cancellationToken);

    public async Task<List<(Guid SongId, Guid SkillId, int RequiredCount)>> GetSongDefaultRequirementsAsync(
        IReadOnlyCollection<Guid> songIds, CancellationToken cancellationToken)
    {
        var vocal = await DbContext.SongVocalRequirements
            .Where(x => songIds.Contains(x.SongId) && x.IsMandatory && x.Skill.IsActive && x.Skill.CategoryId != SkillCategoryIds.Vocal)
            .Select(x => new { x.SongId, x.SkillId, x.RequiredCount })
            .ToListAsync(cancellationToken);
        var instrument = await DbContext.SongInstrumentRequirements
            .Where(x => songIds.Contains(x.SongId) && x.IsMandatory && x.Skill.IsActive)
            .Select(x => new { x.SongId, x.SkillId, x.RequiredCount })
            .ToListAsync(cancellationToken);

        // Expression trees cannot build tuples, so rows are shaped in memory.
        return vocal.Concat(instrument).Select(x => (x.SongId, x.SkillId, x.RequiredCount)).ToList();
    }

    public async Task<List<(Guid SlotId, Guid SkillId, int RequiredCount)>> GetSlotDefaultRequirementsAsync(
        IReadOnlyCollection<Guid> slotIds, CancellationToken cancellationToken) =>
        (await DbContext.LiturgicalSlotRequirements
            .Where(x => slotIds.Contains(x.SlotId) && x.Skill.IsActive)
            .Select(x => new { x.SlotId, x.SkillId, x.RequiredCount })
            .ToListAsync(cancellationToken))
        .Select(x => (x.SlotId, x.SkillId, x.RequiredCount))
        .ToList();

    public Task<Dictionary<Guid, int>> GetWholeServiceRequirementsAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.EventPersonnelRequirements
            .Where(x => x.EventId == eventId)
            .ToDictionaryAsync(x => x.SkillId, x => x.RequiredCount, cancellationToken);
}
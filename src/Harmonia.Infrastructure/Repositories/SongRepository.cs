using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class SongRepository(HarmoniaDbContext dbContext)
    : GenericRepository<Song>(dbContext), ISongRepository
{
    public async Task<PagedList<Song>> SearchAsync(
        string? keyword, SearchSongsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.Songs
            .AsNoTracking()
            .Where(x => x.IsActive);

        // ponytail: LIKE '%keyword%' scans the table; add a full-text index if the library grows large.
        if (keyword is not null)
        {
            const string collation = HarmoniaDbContext.SearchCollation;
            query = query.Where(x => EF.Functions.Collate(x.Title, collation).Contains(keyword)
                || (x.Composer != null && EF.Functions.Collate(x.Composer, collation).Contains(keyword))
                || (x.Lyricist != null && EF.Functions.Collate(x.Lyricist, collation).Contains(keyword))
                || x.MusicMaterials.Any(m => m.IsActive && m.LyricsText != null
                    && EF.Functions.Collate(m.LyricsText, collation).Contains(keyword)));
        }

        query = WhereClassifiedAs(query, ClassificationTarget.LiturgicalSeason, filter.LiturgicalSeasonId);
        query = WhereClassifiedAs(query, ClassificationTarget.MassType, filter.MassTypeId);
        query = WhereClassifiedAs(query, ClassificationTarget.CeremonyType, filter.CeremonyTypeId);
        query = WhereClassifiedAs(query, ClassificationTarget.LiturgicalSlot, filter.LiturgicalSlotId);

        if (filter.SongThemeId is { } themeId)
        {
            var themeIds = await GetThemeWithDescendantsAsync(themeId, cancellationToken);
            query = query.Where(x => x.Classifications.Any(
                c => c.TargetType == ClassificationTarget.SongTheme && themeIds.Contains(c.TargetId)));
        }

        if (filter.SkillId is { } skillId)
        {
            query = query.Where(x => x.VocalRequirements.Any(r => r.SkillId == skillId)
                || x.InstrumentRequirements.Any(r => r.SkillId == skillId));
        }

        if (filter.IsClassified is { } isClassified)
        {
            query = query.Where(x => x.Classifications.Any() == isClassified);
        }

        if (filter.MissingMaterialType is { } missingType)
        {
            query = query.Where(x => !x.MusicMaterials.Any(m => m.IsActive && m.MaterialType == missingType));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Song>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<List<(Song Song, bool MatchesSeason, bool MatchesEventType, bool MatchesSlot, bool HasSeason, bool HasEventType, bool HasSlot)>> GetSuggestionMatchesAsync(
        Guid? liturgicalSeasonId, Guid? massTypeId, Guid? ceremonyTypeId, Guid? slotId, CancellationToken cancellationToken)
    {
        const ClassificationTarget season = ClassificationTarget.LiturgicalSeason, slot = ClassificationTarget.LiturgicalSlot;

        var rows = await DbContext.Songs
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Where(s => liturgicalSeasonId == null
                || !s.Classifications.Any(c => c.TargetType == season)
                || s.Classifications.Any(c => c.TargetType == season && c.TargetId == liturgicalSeasonId))
            .Where(s => slotId == null
                || !s.Classifications.Any(c => c.TargetType == slot)
                || s.Classifications.Any(c => c.TargetType == slot && c.TargetId == slotId))
            .Select(s => new
            {
                Song = s,
                MatchesSeason = s.Classifications.Any(c => c.TargetType == season && c.TargetId == liturgicalSeasonId),
                MatchesEventType = s.Classifications.Any(c =>
                    (c.TargetType == ClassificationTarget.MassType && c.TargetId == massTypeId)
                    || (c.TargetType == ClassificationTarget.CeremonyType && c.TargetId == ceremonyTypeId)),
                MatchesSlot = s.Classifications.Any(c => c.TargetType == slot && c.TargetId == slotId),
                HasSeason = s.Classifications.Any(c => c.TargetType == season),
                HasEventType = s.Classifications.Any(c =>
                    c.TargetType == ClassificationTarget.MassType || c.TargetType == ClassificationTarget.CeremonyType),
                HasSlot = s.Classifications.Any(c => c.TargetType == slot),
            })
            .Where(x => x.MatchesSeason || x.MatchesEventType || x.MatchesSlot)
            .ToListAsync(cancellationToken);

        // Expression trees cannot build tuples, so rows are shaped in memory.
        return rows.Select(x => (x.Song, x.MatchesSeason, x.MatchesEventType, x.MatchesSlot, x.HasSeason, x.HasEventType, x.HasSlot)).ToList();
    }

    public Task<bool> ExistsByTitleAsync(
        string title, string? composer, Guid? excludeSongId, CancellationToken cancellationToken) =>
        DbContext.Songs.AnyAsync(
            x => x.IsActive && x.Title == title && x.Composer == composer && x.Id != excludeSongId,
            cancellationToken);

    public async Task<Dictionary<Guid, SongStats>> GetStatsAsync(
        IReadOnlyCollection<Guid> ids, DateOnly today, CancellationToken cancellationToken)
    {
        // Same events as GetUsageAsync, so the library and the report agree.
        var events = DbContext.LiturgicalEvents
            .Where(e => e.Status == EventStatus.Published && e.EventDate < today);

        var rows = await DbContext.Songs
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                MaterialTypes = s.MusicMaterials.Where(m => m.IsActive).Select(m => m.MaterialType).ToList(),
                IsClassified = s.Classifications.Any(),
                TimesUsed = events.Count(e => e.SongLists.Any(
                    l => l.Status == SongListStatus.Approved && l.Items.Any(i => i.SongId == s.Id))),
                LastUsedDate = events
                    .Where(e => e.SongLists.Any(
                        l => l.Status == SongListStatus.Approved && l.Items.Any(i => i.SongId == s.Id)))
                    .Max(e => (DateOnly?)e.EventDate),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.Id,
            x => new SongStats(
                Enum.GetValues<MaterialType>().ToDictionary(t => t, t => x.MaterialTypes.Count(m => m == t)),
                x.IsClassified, x.TimesUsed, x.LastUsedDate));
    }

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
        await DbContext.SongListItems.AnyAsync(x => x.SongId == id, cancellationToken)
        || await DbContext.RehearsalSongs.AnyAsync(x => x.SongId == id, cancellationToken)
        || await DbContext.PracticeAssignments.AnyAsync(x => x.SongId == id, cancellationToken);

    public Task<Song?> GetWithClassificationAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.Songs
            .Include(x => x.Classifications)
            .Include(x => x.VocalRequirements).ThenInclude(x => x.Skill)
            .Include(x => x.InstrumentRequirements).ThenInclude(x => x.Skill)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Dictionary<Guid, (string Name, bool IsActive)>> GetClassificationTargetsAsync(
        ClassificationTarget targetType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        // The five lookups share the shape Id / Name / IsActive but no base type, hence one branch each.
        var rows = targetType switch
        {
            ClassificationTarget.LiturgicalSeason => await DbContext.LiturgicalSeasons.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(cancellationToken),
            ClassificationTarget.MassType => await DbContext.MassTypes.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(cancellationToken),
            ClassificationTarget.CeremonyType => await DbContext.CeremonyTypes.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(cancellationToken),
            ClassificationTarget.SongTheme => await DbContext.SongThemes.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(cancellationToken),
            ClassificationTarget.LiturgicalSlot => await DbContext.LiturgicalSlots.AsNoTracking()
                .Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Unknown classification target."),
        };

        return rows.ToDictionary(x => x.Id, x => (x.Name, x.IsActive));
    }

    public Task<Dictionary<Guid, Skill>> GetSkillsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        DbContext.Skills.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

    public async Task<PagedList<(Song Song, int TimesUsed, DateOnly? LastUsedDate)>> GetUsageAsync(
        SearchReportsRequest filter, DateOnly today, CancellationToken cancellationToken)
    {
        var events = DbContext.LiturgicalEvents
            .Where(e => e.Status == EventStatus.Published && e.EventDate < today);

        if (filter.EventId is { } eventId)
        {
            events = events.Where(e => e.Id == eventId);
        }

        if (filter.LiturgicalSeasonId is { } seasonId)
        {
            events = events.Where(e => e.LiturgicalSeasonId == seasonId);
        }

        if (filter.FromDate is { } fromDate)
        {
            events = events.Where(e => e.EventDate >= fromDate);
        }

        if (filter.ToDate is { } toDate)
        {
            events = events.Where(e => e.EventDate <= toDate);
        }

        // Counting events rather than items, so a song sung twice in one event counts once.
        var query = DbContext.Songs
            .AsNoTracking()
            .Select(s => new
            {
                Song = s,
                TimesUsed = events.Count(e => e.SongLists.Any(
                    l => l.Status == SongListStatus.Approved && l.Items.Any(i => i.SongId == s.Id))),
                LastUsedDate = events
                    .Where(e => e.SongLists.Any(
                        l => l.Status == SongListStatus.Approved && l.Items.Any(i => i.SongId == s.Id)))
                    .Max(e => (DateOnly?)e.EventDate),
            })
            .Where(x => x.Song.IsActive || x.TimesUsed > 0);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.TimesUsed)
            .ThenBy(x => x.Song.Title)
            .ThenBy(x => x.Song.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        // Expression trees cannot build tuples, so the page is shaped in memory.
        return new PagedList<(Song, int, DateOnly?)>(
            rows.Select(x => (x.Song, x.TimesUsed, x.LastUsedDate)).ToList(),
            filter.PageNumber, filter.PageSize, totalCount);
    }

    /// <summary>The theme and every theme below it, so filtering by "Mary" also finds songs tagged "Rosary".</summary>
    private async Task<List<Guid>> GetThemeWithDescendantsAsync(Guid themeId, CancellationToken cancellationToken)
    {
        // A few dozen rows: loading the tree beats a recursive CTE.
        var childrenByParent = (await DbContext.SongThemes.AsNoTracking()
                .Where(x => x.ParentId != null).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken))
            .ToLookup(x => x.ParentId!.Value, x => x.Id);

        var result = new List<Guid> { themeId };
        for (var i = 0; i < result.Count; i++)
            result.AddRange(childrenByParent[result[i]]);

        return result;
    }

    private static IQueryable<Song> WhereClassifiedAs(IQueryable<Song> query, ClassificationTarget targetType, Guid? targetId) =>
        targetId is { } id
            ? query.Where(x => x.Classifications.Any(c => c.TargetType == targetType && c.TargetId == id))
            : query;
}

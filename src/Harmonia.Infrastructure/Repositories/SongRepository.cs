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
            query = query.Where(x => x.Title.Contains(keyword)
                || (x.Composer != null && x.Composer.Contains(keyword))
                || (x.Lyricist != null && x.Lyricist.Contains(keyword)));
        }

        query = WhereClassifiedAs(query, ClassificationTarget.LiturgicalSeason, filter.LiturgicalSeasonId);
        query = WhereClassifiedAs(query, ClassificationTarget.MassType, filter.MassTypeId);
        query = WhereClassifiedAs(query, ClassificationTarget.CeremonyType, filter.CeremonyTypeId);
        query = WhereClassifiedAs(query, ClassificationTarget.SongTheme, filter.SongThemeId);

        if (filter.SkillId is { } skillId)
        {
            query = query.Where(x => x.VocalRequirements.Any(r => r.SkillId == skillId)
                || x.InstrumentRequirements.Any(r => r.SkillId == skillId));
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

    public Task<bool> ExistsByTitleAsync(
        string title, string? composer, Guid? excludeSongId, CancellationToken cancellationToken) =>
        DbContext.Songs.AnyAsync(
            x => x.IsActive && x.Title == title && x.Composer == composer && x.Id != excludeSongId,
            cancellationToken);

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
        // The four lookups share the shape Id / Name / IsActive but no base type, hence one branch each.
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
            _ => throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Unknown classification target."),
        };

        return rows.ToDictionary(x => x.Id, x => (x.Name, x.IsActive));
    }

    public Task<Dictionary<Guid, Skill>> GetSkillsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        DbContext.Skills.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

    private static IQueryable<Song> WhereClassifiedAs(IQueryable<Song> query, ClassificationTarget targetType, Guid? targetId) =>
        targetId is { } id
            ? query.Where(x => x.Classifications.Any(c => c.TargetType == targetType && c.TargetId == id))
            : query;
}

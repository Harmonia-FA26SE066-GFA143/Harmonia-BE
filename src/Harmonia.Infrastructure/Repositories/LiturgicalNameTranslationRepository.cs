using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class LiturgicalNameTranslationRepository(HarmoniaDbContext dbContext)
    : GenericRepository<LiturgicalNameTranslation>(dbContext), ILiturgicalNameTranslationRepository
{
    public async Task<PagedList<LiturgicalNameTranslation>> SearchAsync(
        string? keyword, LiturgicalNameKind? kind, PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.LiturgicalNameTranslations.AsNoTracking();

        if (kind is not null) query = query.Where(x => x.Kind == kind);

        if (keyword is not null)
        {
            const string collation = HarmoniaDbContext.SearchCollation;
            query = query.Where(x => EF.Functions.Collate(x.EnglishName, collation).Contains(keyword)
                || EF.Functions.Collate(x.VietnameseName, collation).Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.EnglishName)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<LiturgicalNameTranslation>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public Task<bool> ExistsAsync(
        LiturgicalNameKind kind, string englishName, Guid? excludeId, CancellationToken cancellationToken) =>
        DbContext.LiturgicalNameTranslations.AnyAsync(
            x => x.Kind == kind && x.EnglishName == englishName && x.Id != excludeId, cancellationToken);

    public async Task<Dictionary<(LiturgicalNameKind Kind, string EnglishName), string>> GetAllAsync(
        CancellationToken cancellationToken) =>
        (await DbContext.LiturgicalNameTranslations.AsNoTracking()
            .Select(x => new { x.Kind, x.EnglishName, x.VietnameseName })
            .ToListAsync(cancellationToken))
        .ToDictionary(x => (x.Kind, x.EnglishName), x => x.VietnameseName);

    public async Task<List<(LiturgicalNameKind Kind, string EnglishName)>> GetCalendarNamesAsync(
        CancellationToken cancellationToken)
    {
        var days = DbContext.LiturgicalDays.AsNoTracking();
        var celebrations = await days.Select(x => x.CelebrationName).Distinct().ToListAsync(cancellationToken);
        var ranks = await days.Where(x => x.Rank != null).Select(x => x.Rank!).Distinct().ToListAsync(cancellationToken);
        var seasons = await days.Where(x => x.SeasonName != null).Select(x => x.SeasonName!).Distinct()
            .ToListAsync(cancellationToken);

        return celebrations.Select(x => (LiturgicalNameKind.Celebration, x))
            .Concat(ranks.Select(x => (LiturgicalNameKind.Rank, x)))
            .Concat(seasons.Select(x => (LiturgicalNameKind.Season, x)))
            .ToList();
    }
}

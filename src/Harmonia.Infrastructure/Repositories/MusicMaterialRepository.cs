using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class MusicMaterialRepository(HarmoniaDbContext dbContext)
    : GenericRepository<MusicMaterial>(dbContext), IMusicMaterialRepository
{
    public async Task<PagedList<MusicMaterial>> GetActiveBySongAsync(
        Guid songId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.MusicMaterials
            .AsNoTracking()
            .Where(x => x.SongId == songId && x.IsActive);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.TargetSkill)
            .OrderBy(x => x.MaterialType)
            .ThenBy(x => x.Title)
            .ThenBy(x => x.Id)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MusicMaterial>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public async Task<PagedList<MusicMaterial>> GetActiveForMemberAsync(
        Guid memberId, string? keyword, SearchMusicMaterialsRequest filter, CancellationToken cancellationToken)
    {
        var approvedSkillIds = DbContext.MemberSkills
            .Where(s => s.MemberId == memberId && s.Status == ApprovalStatus.Approved)
            .Select(s => s.SkillId);

        var query = DbContext.MusicMaterials
            .AsNoTracking()
            .Where(x => x.IsActive && x.Song.IsActive
                && (x.TargetSkillId == null || approvedSkillIds.Contains(x.TargetSkillId.Value)));

        // ponytail: LIKE '%keyword%' scans the table; add a full-text index if the library grows large.
        if (keyword is not null) query = query.Where(x => x.Song.Title.Contains(keyword) || x.Title.Contains(keyword));
        if (filter.SongId is { } songId) query = query.Where(x => x.SongId == songId);
        if (filter.SkillId is { } skillId) query = query.Where(x => x.TargetSkillId == skillId);
        if (filter.MaterialType is { } materialType) query = query.Where(x => x.MaterialType == materialType);

        if (filter.LiturgicalSeasonId is { } seasonId)
        {
            query = query.Where(x => x.Song.Classifications.Any(
                c => c.TargetType == ClassificationTarget.LiturgicalSeason && c.TargetId == seasonId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.TargetSkill)
            .OrderBy(x => x.Song.Title)
            .ThenBy(x => x.MaterialType)
            .ThenBy(x => x.Title)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MusicMaterial>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}

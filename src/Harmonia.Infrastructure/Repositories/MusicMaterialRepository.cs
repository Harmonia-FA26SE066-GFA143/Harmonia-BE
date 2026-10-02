using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
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
}

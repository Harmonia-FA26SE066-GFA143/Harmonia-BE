using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class SongRepository(HarmoniaDbContext dbContext)
    : GenericRepository<Song>(dbContext), ISongRepository
{
    public async Task<PagedList<Song>> SearchAsync(
        string? keyword, PagingRequest paging, CancellationToken cancellationToken)
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

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<Song>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public Task<bool> ExistsByTitleAsync(
        string title, string? composer, Guid? excludeSongId, CancellationToken cancellationToken) =>
        DbContext.Songs.AnyAsync(
            x => x.IsActive && x.Title == title && x.Composer == composer && x.Id != excludeSongId,
            cancellationToken);
}

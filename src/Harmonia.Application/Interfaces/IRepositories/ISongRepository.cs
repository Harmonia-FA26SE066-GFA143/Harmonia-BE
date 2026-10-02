using Harmonia.Application.Common.Models;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ISongRepository : IGenericRepository<Song>
{
    /// <summary>
    /// One page of active songs ordered by title. <paramref name="keyword"/> matches title,
    /// composer or lyricist; null returns every active song.
    /// </summary>
    Task<PagedList<Song>> SearchAsync(string? keyword, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// True if another active song has the same title and composer (a null composer matches only null).
    /// Deleted songs are ignored, so a deleted title can be created again.
    /// </summary>
    Task<bool> ExistsByTitleAsync(string title, string? composer, Guid? excludeSongId, CancellationToken cancellationToken);
}

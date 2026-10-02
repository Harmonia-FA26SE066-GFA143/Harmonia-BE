using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ISongRepository : IGenericRepository<Song>
{
    /// <summary>
    /// One page of active songs ordered by title. <paramref name="keyword"/> matches title,
    /// composer or lyricist; null returns every active song. The classification ids of
    /// <paramref name="filter"/> narrow the result further when set.
    /// </summary>
    Task<PagedList<Song>> SearchAsync(string? keyword, SearchSongsRequest filter, CancellationToken cancellationToken);

    /// <summary>
    /// True if another active song has the same title and composer (a null composer matches only null).
    /// Deleted songs are ignored, so a deleted title can be created again.
    /// </summary>
    Task<bool> ExistsByTitleAsync(string title, string? composer, Guid? excludeSongId, CancellationToken cancellationToken);

    /// <summary>Tracked song with its classifications and vocal / instrument requirements (each with its Skill).</summary>
    Task<Song?> GetWithClassificationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Name and active state of the given ids in the lookup table <paramref name="targetType"/> points at.
    /// Ids that do not exist are simply missing from the result.
    /// </summary>
    Task<Dictionary<Guid, (string Name, bool IsActive)>> GetClassificationTargetsAsync(
        ClassificationTarget targetType, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Tracked skills keyed by id. Missing ids are absent from the result.</summary>
    Task<Dictionary<Guid, Skill>> GetSkillsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}

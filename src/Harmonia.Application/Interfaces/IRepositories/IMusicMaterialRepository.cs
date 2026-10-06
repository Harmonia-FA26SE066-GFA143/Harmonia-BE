using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMusicMaterialRepository : IGenericRepository<MusicMaterial>
{
    /// <summary>
    /// Read-only page of the song's active materials ordered by type then title,
    /// with <see cref="MusicMaterial.TargetSkill"/> loaded.
    /// </summary>
    Task<PagedList<MusicMaterial>> GetActiveBySongAsync(Guid songId, PagingRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of active materials of active songs that are meant for everyone or for one of the
    /// member's approved skills, ordered by song title, type then title, with <see cref="MusicMaterial.TargetSkill"/> loaded
    /// and <see cref="MusicMaterial.LearningProgresses"/> holding only this member's row, if any.
    /// <paramref name="keyword"/> matches song or material title; null skips it. The other filters of
    /// <paramref name="filter"/> narrow the result further when set.
    /// </summary>
    Task<PagedList<MusicMaterial>> GetActiveForMemberAsync(
        Guid memberId, string? keyword, SearchMusicMaterialsRequest filter, CancellationToken cancellationToken);

    /// <summary>
    /// True if the material is one <see cref="GetActiveForMemberAsync"/> would list for the member:
    /// active, of an active song, and meant for everyone or for one of the member's approved skills.
    /// </summary>
    Task<bool> IsVisibleToMemberAsync(Guid materialId, Guid memberId, CancellationToken cancellationToken);
}

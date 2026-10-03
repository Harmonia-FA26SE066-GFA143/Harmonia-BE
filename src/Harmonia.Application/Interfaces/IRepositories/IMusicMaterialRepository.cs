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
    /// member's approved skills, ordered by song title, type then title, with <see cref="MusicMaterial.TargetSkill"/> loaded.
    /// <paramref name="keyword"/> matches song or material title; null skips it. The other filters of
    /// <paramref name="filter"/> narrow the result further when set.
    /// </summary>
    Task<PagedList<MusicMaterial>> GetActiveForMemberAsync(
        Guid memberId, string? keyword, SearchMusicMaterialsRequest filter, CancellationToken cancellationToken);
}

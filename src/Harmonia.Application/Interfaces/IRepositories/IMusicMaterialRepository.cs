using Harmonia.Application.Common.Models;
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
    /// <paramref name="songId"/> null means every song.
    /// </summary>
    Task<PagedList<MusicMaterial>> GetActiveForMemberAsync(
        Guid memberId, Guid? songId, PagingRequest paging, CancellationToken cancellationToken);
}

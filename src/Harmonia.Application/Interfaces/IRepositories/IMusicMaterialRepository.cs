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
}

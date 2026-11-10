using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ITimedLyricsRepository : IGenericRepository<TimedLyrics>
{
    /// <summary>Tracked, so the caller can replace its lines and save.</summary>
    Task<TimedLyrics?> GetByAudioMaterialIdAsync(Guid audioMaterialId, CancellationToken cancellationToken);
}

using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILyricsExtractionJobRepository : IGenericRepository<LyricsExtractionJob>
{
    /// <summary>The Queued or Running job of the material, if any. Read-only.</summary>
    Task<LyricsExtractionJob?> GetActiveByMaterialAsync(Guid musicMaterialId, CancellationToken cancellationToken);

    /// <summary>The newest job of the material that has not expired at <paramref name="now"/>. Read-only.</summary>
    Task<LyricsExtractionJob?> GetLatestByMaterialAsync(Guid musicMaterialId, DateTime now, CancellationToken cancellationToken);

    /// <summary>Ids of Queued jobs, oldest first.</summary>
    Task<List<Guid>> ListQueuedIdsAsync(CancellationToken cancellationToken);

    /// <summary>Marks every Running job Failed with <paramref name="errorCode"/>, straight in the database.</summary>
    Task FailRunningAsync(string errorCode, DateTime now, DateTime expiresAt, CancellationToken cancellationToken);

    /// <summary>Deletes jobs that expired before <paramref name="now"/>, straight in the database.</summary>
    Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken);
}

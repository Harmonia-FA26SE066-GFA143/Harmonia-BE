using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class LyricsExtractionJobRepository(HarmoniaDbContext dbContext)
    : GenericRepository<LyricsExtractionJob>(dbContext), ILyricsExtractionJobRepository
{
    public Task<LyricsExtractionJob?> GetActiveByMaterialAsync(Guid musicMaterialId, CancellationToken cancellationToken) =>
        DbContext.LyricsExtractionJobs.AsNoTracking()
            .Where(x => x.MusicMaterialId == musicMaterialId
                && (x.Status == LyricsExtractionStatus.Queued || x.Status == LyricsExtractionStatus.Running))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LyricsExtractionJob?> GetLatestByMaterialAsync(
        Guid musicMaterialId, DateTime now, CancellationToken cancellationToken) =>
        DbContext.LyricsExtractionJobs.AsNoTracking()
            .Where(x => x.MusicMaterialId == musicMaterialId && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<Guid>> ListQueuedIdsAsync(CancellationToken cancellationToken) =>
        DbContext.LyricsExtractionJobs
            .Where(x => x.Status == LyricsExtractionStatus.Queued)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task FailRunningAsync(string errorCode, DateTime now, DateTime expiresAt, CancellationToken cancellationToken) =>
        DbContext.LyricsExtractionJobs
            .Where(x => x.Status == LyricsExtractionStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, LyricsExtractionStatus.Failed)
                .SetProperty(x => x.ErrorCode, errorCode)
                .SetProperty(x => x.CompletedAt, now)
                .SetProperty(x => x.ExpiresAt, expiresAt), cancellationToken);

    public Task DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken) =>
        DbContext.LyricsExtractionJobs.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
}

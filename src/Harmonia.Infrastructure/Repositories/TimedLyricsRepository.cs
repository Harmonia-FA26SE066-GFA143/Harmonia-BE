using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class TimedLyricsRepository(HarmoniaDbContext dbContext)
    : GenericRepository<TimedLyrics>(dbContext), ITimedLyricsRepository
{
    public Task<TimedLyrics?> GetByAudioMaterialIdAsync(Guid audioMaterialId, CancellationToken cancellationToken) =>
        DbContext.TimedLyrics.FirstOrDefaultAsync(x => x.AudioMaterialId == audioMaterialId, cancellationToken);
}

using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class RehearsalRecordingRepository(HarmoniaDbContext dbContext)
    : GenericRepository<RehearsalRecording>(dbContext), IRehearsalRecordingRepository
{
    public Task<List<RehearsalRecording>> GetByRehearsalAsync(Guid rehearsalId, CancellationToken cancellationToken) =>
        DbContext.RehearsalRecordings
            .AsNoTracking()
            .Include(x => x.Song)
            .Where(x => x.RehearsalId == rehearsalId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
}

using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class MaterialLearningProgressRepository(HarmoniaDbContext dbContext)
    : GenericRepository<MaterialLearningProgress>(dbContext), IMaterialLearningProgressRepository
{
    public async Task<MaterialLearningProgress> UpsertAsync(
        Guid memberId, Guid materialId, LearningStatus status, CancellationToken cancellationToken)
    {
        var progress = await FindAsync(memberId, materialId, cancellationToken);
        if (progress is null)
        {
            progress = new MaterialLearningProgress { Id = Guid.NewGuid(), MemberId = memberId, MaterialId = materialId };
            DbContext.MaterialLearningProgresses.Add(progress);
        }

        progress.Status = status;
        progress.UpdatedAt = DateTime.Now;

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return progress;
        }
        catch (DbUpdateException) when (DbContext.Entry(progress).State == EntityState.Added)
        {
            // Another request inserted the row between our read and our insert, so the unique
            // (MemberId, MaterialId) index rejected ours. Apply this mark to that row instead.
            // Checking for the row rather than the provider's error number keeps this provider-neutral.
            DbContext.Entry(progress).State = EntityState.Detached;
            var existing = await FindAsync(memberId, materialId, cancellationToken);
            if (existing is null) throw;

            existing.Status = status;
            existing.UpdatedAt = DateTime.Now;
            await DbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }
    }

    private Task<MaterialLearningProgress?> FindAsync(Guid memberId, Guid materialId, CancellationToken cancellationToken) =>
        DbContext.MaterialLearningProgresses
            .FirstOrDefaultAsync(x => x.MemberId == memberId && x.MaterialId == materialId, cancellationToken);
}

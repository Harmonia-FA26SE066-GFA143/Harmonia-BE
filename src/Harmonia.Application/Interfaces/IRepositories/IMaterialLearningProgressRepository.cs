using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMaterialLearningProgressRepository : IGenericRepository<MaterialLearningProgress>
{
    /// <summary>
    /// Sets the member's status on the material and saves: creates the row on first use, updates it otherwise.
    /// A concurrent first mark that inserts the row in between is absorbed by updating that row instead.
    /// </summary>
    Task<MaterialLearningProgress> UpsertAsync(
        Guid memberId, Guid materialId, LearningStatus status, CancellationToken cancellationToken);
}

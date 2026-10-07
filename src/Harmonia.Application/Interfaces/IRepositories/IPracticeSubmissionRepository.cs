using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IPracticeSubmissionRepository : IGenericRepository<PracticeSubmission>
{
    /// <summary>
    /// Inserts the submission and saves. Returns false when a concurrent request already inserted the same
    /// attempt number for this assignment and member, so the unique index rejected this one.
    /// </summary>
    Task<bool> TryAddAsync(PracticeSubmission submission, CancellationToken cancellationToken);
}

using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class PracticeSubmissionRepository(HarmoniaDbContext dbContext)
    : GenericRepository<PracticeSubmission>(dbContext), IPracticeSubmissionRepository
{
    public async Task<bool> TryAddAsync(PracticeSubmission submission, CancellationToken cancellationToken)
    {
        DbContext.PracticeSubmissions.Add(submission);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (DbContext.Entry(submission).State == EntityState.Added)
        {
            // Another request took this attempt number between our read and our insert, so the unique
            // (assignment, member, attempt) index rejected ours. Checking for that row rather than the
            // provider's error number keeps this provider-neutral; any other failure is rethrown.
            DbContext.Entry(submission).State = EntityState.Detached;
            var taken = await DbContext.PracticeSubmissions.AnyAsync(
                x => x.PracticeAssignmentId == submission.PracticeAssignmentId
                    && x.MemberId == submission.MemberId
                    && x.AttemptNo == submission.AttemptNo,
                cancellationToken);
            if (taken) return false;
            throw;
        }
    }
}

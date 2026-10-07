using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
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

    public async Task<PagedList<PracticeSubmission>> SearchAsync(
        Guid? assignmentId, SearchPracticeSubmissionsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.PracticeSubmissions.AsNoTracking();

        if (assignmentId is { } id) query = query.Where(x => x.PracticeAssignmentId == id);

        if (!filter.AllAttempts)
        {
            query = query.Where(x => !DbContext.PracticeSubmissions.Any(newer =>
                newer.PracticeAssignmentId == x.PracticeAssignmentId
                && newer.MemberId == x.MemberId
                && newer.AttemptNo > x.AttemptNo));
        }

        if (filter.Status is { } status) query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = assignmentId is null
            ? query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id)
            : query.OrderBy(x => x.Member.User.FullName).ThenByDescending(x => x.AttemptNo).ThenBy(x => x.Id);

        var items = await WithMemberAndAssignment(ordered)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<PracticeSubmission>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<PracticeSubmission?> GetWithMemberAsync(Guid id, CancellationToken cancellationToken) =>
        WithMemberAndAssignment(DbContext.PracticeSubmissions.AsNoTracking())
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PracticeSubmission?> GetForReviewAsync(Guid id, CancellationToken cancellationToken) =>
        WithMemberAndAssignment(DbContext.PracticeSubmissions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> IsLatestAttemptAsync(PracticeSubmission submission, CancellationToken cancellationToken) =>
        !await DbContext.PracticeSubmissions.AnyAsync(
            x => x.PracticeAssignmentId == submission.PracticeAssignmentId
                && x.MemberId == submission.MemberId
                && x.AttemptNo > submission.AttemptNo,
            cancellationToken);

    public async Task<bool> TrySaveReviewAsync(
        PracticeSubmission submission, PracticeFeedback feedback, CancellationToken cancellationToken)
    {
        // Added explicitly: a new child with its Id already set, reached only through the tracked parent's
        // collection, would be taken for an existing row and UPDATEd instead of inserted.
        DbContext.PracticeFeedbacks.Add(feedback);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Status is a concurrency token: the UPDATE matched no row because another review got there first.
            // The save ran in one transaction, so our feedback was rolled back with it; detach both.
            DbContext.Entry(feedback).State = EntityState.Detached;
            DbContext.Entry(submission).State = EntityState.Detached;
            return false;
        }
    }

    private static IQueryable<PracticeSubmission> WithMemberAndAssignment(IQueryable<PracticeSubmission> query) =>
        query
            .Include(x => x.Member)
                .ThenInclude(m => m.User)
            .Include(x => x.PracticeAssignment);
}

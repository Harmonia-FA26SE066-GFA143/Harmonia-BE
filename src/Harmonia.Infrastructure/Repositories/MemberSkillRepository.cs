using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class MemberSkillRepository(HarmoniaDbContext dbContext)
    : GenericRepository<MemberSkill>(dbContext), IMemberSkillRepository
{
    public Task<Skill?> GetSkillWithCategoryAsync(Guid skillId, CancellationToken cancellationToken) =>
        DbContext.Skills
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == skillId, cancellationToken);

    public Task<bool> HasActiveDeclarationAsync(Guid memberId, Guid skillId, CancellationToken cancellationToken) =>
        DbContext.MemberSkills.AnyAsync(
            x => x.MemberId == memberId && x.SkillId == skillId && x.Status != ApprovalStatus.Rejected,
            cancellationToken);

    public async Task<bool> TryAddAsync(MemberSkill memberSkill, CancellationToken cancellationToken)
    {
        DbContext.MemberSkills.Add(memberSkill);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (DbContext.Entry(memberSkill).State == EntityState.Added)
        {
            // Another request declared the same skill between our check and our insert, so the filtered
            // unique (MemberId, SkillId) index rejected ours. Checking for that row rather than the
            // provider's error number keeps this provider-neutral; any other failure is rethrown.
            DbContext.Entry(memberSkill).State = EntityState.Detached;
            if (await HasActiveDeclarationAsync(memberSkill.MemberId, memberSkill.SkillId, cancellationToken)) return false;
            throw;
        }
    }

    public Task<MemberSkill?> GetWithSkillAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.MemberSkills
            .AsNoTracking()
            .Include(x => x.Skill)
                .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedList<MemberSkill>> GetByMemberAsync(
        Guid memberId, ApprovalStatus? status, PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.MemberSkills
            .AsNoTracking()
            .Where(x => x.MemberId == memberId);

        if (status is not null) query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Skill)
                .ThenInclude(s => s.Category)
            .OrderByDescending(x => x.DeclaredAt)
            .ThenBy(x => x.Id)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberSkill>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public async Task<PagedList<MemberSkill>> GetPendingAsync(PagingRequest paging, CancellationToken cancellationToken)
    {
        var query = DbContext.MemberSkills
            .AsNoTracking()
            .Where(x => x.Status == ApprovalStatus.Pending);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.Skill)
                .ThenInclude(s => s.Category)
            .Include(x => x.Member)
                .ThenInclude(m => m.User)
            .OrderBy(x => x.DeclaredAt)
            .ThenBy(x => x.Id)
            .Skip((paging.PageNumber - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberSkill>(items, paging.PageNumber, paging.PageSize, totalCount);
    }

    public Task<MemberSkill?> GetForReviewAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.MemberSkills
            .Include(x => x.Skill)
                .ThenInclude(s => s.Category)
            .Include(x => x.Member)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> TrySaveReviewAsync(MemberSkill memberSkill, CancellationToken cancellationToken)
    {
        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Status is a concurrency token: the UPDATE matched no row because another review got there first.
            DbContext.Entry(memberSkill).State = EntityState.Detached;
            return false;
        }
    }
}

using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class MemberProfileRepository(HarmoniaDbContext dbContext)
    : GenericRepository<MemberProfile>(dbContext), IMemberProfileRepository
{
    public Task<MemberProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<PagedList<MemberProfile>> GetLearnersOfMaterialAsync(
        Guid materialId, Guid? targetSkillId, SearchMaterialLearningProgressRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.MemberProfiles
            .AsNoTracking()
            .Where(x => x.Status == MemberStatus.Active);

        if (targetSkillId is { } skillId)
        {
            query = query.Where(x => x.MemberSkills.Any(s => s.SkillId == skillId && s.Status == ApprovalStatus.Approved));
        }

        if (filter.Status is { } status)
        {
            // No row means NotStarted; rows are only ever written as Learned or NeedsPractice.
            query = status == LearningStatus.NotStarted
                ? query.Where(x => !x.LearningProgresses.Any(p => p.MaterialId == materialId))
                : query.Where(x => x.LearningProgresses.Any(p => p.MaterialId == materialId && p.Status == status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.LearningProgresses.Where(p => p.MaterialId == materialId))
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberProfile>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}

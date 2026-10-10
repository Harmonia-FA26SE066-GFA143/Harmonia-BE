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

    public Task<MemberProfile?> GetByUserIdWithApprovedSkillsAsync(Guid userId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .AsNoTracking()
            .Include(x => x.User)
                .ThenInclude(u => u.Role)
            .Include(x => x.MemberSkills.Where(s => s.Status == ApprovalStatus.Approved && s.Skill.IsActive))
                .ThenInclude(s => s.Skill)
                .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<MemberProfile?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<MemberProfile?> GetWithUserAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PagedList<MemberProfile>> SearchAsync(
        string? keyword, SearchMemberProfilesRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.MemberProfiles.AsNoTracking();

        if (keyword is not null)
        {
            query = query.Where(x => x.User.FullName.Contains(keyword) || x.User.Email.Contains(keyword));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Status == status);
        }

        if (filter.SkillId is { } skillId)
        {
            // Same condition as the ApprovedSkills include below, so a match always shows the skill.
            query = query.Where(x => x.MemberSkills.Any(
                s => s.SkillId == skillId && s.Status == ApprovalStatus.Approved && s.Skill.IsActive));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(x => x.User)
            .Include(x => x.MemberSkills.Where(s => s.Status == ApprovalStatus.Approved && s.Skill.IsActive))
                .ThenInclude(s => s.Skill)
                .ThenInclude(s => s.Category)
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberProfile>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

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
            .Include(x => x.User)
            .Include(x => x.LearningProgresses.Where(p => p.MaterialId == materialId))
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberProfile>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<List<MemberProfile>> GetAttendanceRosterAsync(Guid rehearsalId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .AsNoTracking()
            .Where(x => x.Status == MemberStatus.Active || x.RehearsalAttendances.Any(a => a.RehearsalId == rehearsalId))
            .Include(x => x.User)
            .Include(x => x.RehearsalAttendances.Where(a => a.RehearsalId == rehearsalId))
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<List<MemberProfile>> GetActiveForEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        DbContext.MemberProfiles
            .AsNoTracking()
            .Where(x => x.Status == MemberStatus.Active)
            .Include(x => x.User)
            .Include(x => x.MemberSkills.Where(s => s.Status == ApprovalStatus.Approved))
            .Include(x => x.EventParticipations.Where(p => p.EventId == eventId))
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
}

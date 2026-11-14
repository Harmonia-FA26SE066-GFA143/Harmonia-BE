using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class MemberRegistrationRepository(HarmoniaDbContext dbContext)
    : GenericRepository<MemberRegistration>(dbContext), IMemberRegistrationRepository
{
    public Task<MemberRegistration?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        DbContext.MemberRegistrations
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public async Task<PagedList<MemberRegistration>> SearchAsync(
        string? keyword, SearchMemberRegistrationsRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.MemberRegistrations.AsNoTracking();

        if (keyword is not null)
        {
            query = query.Where(x => x.FullName.Contains(keyword) || x.Email.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<MemberRegistration>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}

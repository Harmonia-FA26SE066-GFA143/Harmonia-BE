using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
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
}

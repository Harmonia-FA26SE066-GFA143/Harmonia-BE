using Harmonia.Domain.Entities;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IMemberProfileRepository : IGenericRepository<MemberProfile>
{
    /// <summary>Read-only, with <see cref="MemberProfile.User"/> loaded.</summary>
    Task<MemberProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}

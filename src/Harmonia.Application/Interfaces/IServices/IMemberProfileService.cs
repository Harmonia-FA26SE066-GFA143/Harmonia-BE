using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

public interface IMemberProfileService
{
    /// <summary>Profile of the calling user; fails with MEMBER_NOT_FOUND if the account has none.</summary>
    Task<Result<MemberProfileDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken);
}

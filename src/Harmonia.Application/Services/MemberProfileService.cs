using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Services;

public class MemberProfileService(
    IMemberProfileRepository memberProfileRepository,
    IMapper mapper) : IMemberProfileService
{
    public async Task<Result<MemberProfileDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);

        return profile is null
            ? Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound)
            : Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }
}

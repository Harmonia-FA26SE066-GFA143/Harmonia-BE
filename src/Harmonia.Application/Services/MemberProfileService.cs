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
    public async Task<Result<MemberProfileDetailDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdWithApprovedSkillsAsync(userId, cancellationToken);

        return profile is null
            ? Result<MemberProfileDetailDto>.Failure(ErrorCodes.MemberNotFound)
            : Result<MemberProfileDetailDto>.Success(mapper.Map<MemberProfileDetailDto>(profile));
    }

    public async Task<Result<MemberProfileDto>> UpdateMineAsync(
        Guid userId, UpdateMyMemberProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound);
        }

        profile.User.FullName = request.FullName.Trim();
        profile.User.Phone = NormalizePhone(request.Phone);
        profile.DateOfBirth = request.DateOfBirth;
        await memberProfileRepository.SaveChangesAsync(cancellationToken);

        return Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    public async Task<Result<PagedList<MemberProfileSummaryDto>>> SearchAsync(
        SearchMemberProfilesRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await memberProfileRepository.SearchAsync(keyword, request, cancellationToken);

        return Result<PagedList<MemberProfileSummaryDto>>.Success(new PagedList<MemberProfileSummaryDto>(
            mapper.Map<List<MemberProfileSummaryDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MemberProfileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetWithUserAsync(id, cancellationToken);

        return profile is null
            ? Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound)
            : Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    public async Task<Result<MemberProfileDto>> UpdateAsync(
        Guid id, UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetWithUserAsync(id, cancellationToken);
        if (profile is null)
        {
            return Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound);
        }

        profile.User.Phone = NormalizePhone(request.Phone);
        profile.DateOfBirth = request.DateOfBirth;
        profile.JoinedDate = request.JoinedDate;
        profile.Status = request.Status;
        await memberProfileRepository.SaveChangesAsync(cancellationToken);

        return Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}

using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class MemberSkillService(
    IMemberSkillRepository memberSkillRepository,
    IMemberProfileRepository memberProfileRepository,
    INotificationService notificationService,
    IMapper mapper) : IMemberSkillService
{
    public async Task<Result<MemberSkillDto>> DeclareAsync(
        Guid userId, DeclareMemberSkillRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<MemberSkillDto>.Failure(ErrorCodes.MemberNotFound);
        if (member.Status != MemberStatus.Active) return Result<MemberSkillDto>.Failure(ErrorCodes.MemberNotActive);

        var skill = await memberSkillRepository.GetSkillWithCategoryAsync(request.SkillId, cancellationToken);
        if (skill is null) return Result<MemberSkillDto>.Failure(ErrorCodes.SkillNotFound);

        // A skill in a disabled category is hidden from the lookup, so it counts as inactive too.
        if (!skill.IsActive || !skill.Category.IsActive) return Result<MemberSkillDto>.Failure(ErrorCodes.SkillInactive);

        if (await memberSkillRepository.HasActiveDeclarationAsync(member.Id, skill.Id, cancellationToken))
        {
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillAlreadyDeclared);
        }

        var memberSkill = new MemberSkill
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            SkillId = skill.Id,
            Level = request.Level,
            Status = ApprovalStatus.Pending,
            DeclaredAt = DateTime.UtcNow
        };

        if (!await memberSkillRepository.TryAddAsync(memberSkill, cancellationToken))
        {
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillAlreadyDeclared);
        }

        var saved = await memberSkillRepository.GetWithSkillAsync(memberSkill.Id, cancellationToken);
        return Result<MemberSkillDto>.Success(mapper.Map<MemberSkillDto>(saved));
    }

    public async Task<Result<PagedList<MemberSkillDto>>> GetMineAsync(
        Guid userId, ApprovalStatus? status, PagingRequest paging, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PagedList<MemberSkillDto>>.Failure(ErrorCodes.MemberNotFound);

        var page = await memberSkillRepository.GetByMemberAsync(member.Id, status, paging, cancellationToken);

        return Result<PagedList<MemberSkillDto>>.Success(new PagedList<MemberSkillDto>(
            mapper.Map<List<MemberSkillDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MemberSkillDto>> GetMineByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<MemberSkillDto>.Failure(ErrorCodes.MemberNotFound);

        var memberSkill = await memberSkillRepository.GetWithSkillAsync(id, cancellationToken);

        // Another member's declaration reads as missing rather than forbidden.
        if (memberSkill is null || memberSkill.MemberId != member.Id)
        {
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillNotFound);
        }

        return Result<MemberSkillDto>.Success(mapper.Map<MemberSkillDto>(memberSkill));
    }

    public async Task<Result<PagedList<MemberSkillDetailDto>>> GetPendingAsync(
        PagingRequest paging, CancellationToken cancellationToken)
    {
        var page = await memberSkillRepository.GetPendingAsync(paging, cancellationToken);

        return Result<PagedList<MemberSkillDetailDto>>.Success(new PagedList<MemberSkillDetailDto>(
            mapper.Map<List<MemberSkillDetailDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public Task<Result<MemberSkillDetailDto>> ApproveAsync(
        Guid directorUserId, Guid id, CancellationToken cancellationToken) =>
        ReviewAsync(directorUserId, id, ApprovalStatus.Approved, null, cancellationToken);

    public Task<Result<MemberSkillDetailDto>> RejectAsync(
        Guid directorUserId, Guid id, RejectMemberSkillRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(directorUserId, id, ApprovalStatus.Rejected, request.Reason.Trim(), cancellationToken);

    private async Task<Result<MemberSkillDetailDto>> ReviewAsync(
        Guid directorUserId, Guid id, ApprovalStatus decision, string? rejectReason, CancellationToken cancellationToken)
    {
        var memberSkill = await memberSkillRepository.GetForReviewAsync(id, cancellationToken);
        if (memberSkill is null) return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillNotFound);
        if (memberSkill.Status != ApprovalStatus.Pending)
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillAlreadyReviewed);
        }

        memberSkill.Status = decision;
        memberSkill.ApprovedBy = directorUserId;
        memberSkill.ApprovedAt = DateTime.UtcNow;
        memberSkill.RejectReason = rejectReason;

        // Another director reviewed the row between our read and our save; theirs stands.
        if (!await memberSkillRepository.TrySaveReviewAsync(memberSkill, cancellationToken))
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillAlreadyReviewed);
        }

        var skillName = memberSkill.Skill.Name;
        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.SkillReview,
                decision == ApprovalStatus.Approved ? "Skill approved" : "Skill rejected",
                decision == ApprovalStatus.Approved
                    ? $"Your {skillName} skill has been approved."
                    : $"Your {skillName} skill has been rejected: {rejectReason}",
                [memberSkill.Member.UserId],
                nameof(MemberSkill),
                memberSkill.Id),
            cancellationToken);

        return Result<MemberSkillDetailDto>.Success(mapper.Map<MemberSkillDetailDto>(memberSkill));
    }
}

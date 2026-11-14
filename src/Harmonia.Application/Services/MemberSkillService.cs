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
    IFileStorageService fileStorageService,
    IServiceRosterRepository rosterRepository,
    IChoirScopeService choirScope,
    IGenericRepository<Choir> choirRepository,
    IMapper mapper) : IMemberSkillService
{
    public const long MaxVoiceSizeBytes = 20 * 1024 * 1024;
    public const long MaxClipSizeBytes = 100 * 1024 * 1024;

    private static readonly string[] VoiceExtensions = [".mp3", ".m4a", ".wav"];
    private static readonly string[] ClipExtensions = [".mp4", ".mov"];

    private const string EvidenceFolder = "member-skill-evidence";

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
            page.Items.Select(ToDto<MemberSkillDto>).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
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

        return Result<MemberSkillDto>.Success(ToDto<MemberSkillDto>(memberSkill));
    }

    public async Task<Result<MemberSkillDto>> UploadEvidenceAsync(
        Guid userId, Guid id, FileContent? file, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<MemberSkillDto>.Failure(ErrorCodes.MemberNotFound);
        if (member.Status != MemberStatus.Active) return Result<MemberSkillDto>.Failure(ErrorCodes.MemberNotActive);

        var memberSkill = await memberSkillRepository.GetForReviewAsync(id, cancellationToken);

        // Another member's declaration reads as missing rather than forbidden.
        if (memberSkill is null || memberSkill.MemberId != member.Id)
        {
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillNotFound);
        }

        if (memberSkill.Status != ApprovalStatus.Pending)
        {
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillAlreadyReviewed);
        }

        if (CheckEvidence(file) is { } fileError) return Result<MemberSkillDto>.Failure(fileError);

        var upload = await fileStorageService.UploadAsync(file!.Content, file.FileName, EvidenceFolder, isPrivate: true, cancellationToken);
        if (!upload.IsSuccess) return Result<MemberSkillDto>.Failure(upload.Code!);

        var newPublicId = upload.Value!.PublicId;
        var previousPublicId = memberSkill.EvidencePublicId;

        memberSkill.EvidencePublicId = newPublicId;
        memberSkill.EvidenceFileName = Path.GetFileName(file.FileName);
        memberSkill.EvidenceSizeBytes = file.Length;
        memberSkill.EvidenceUploadedAt = DateTime.UtcNow;

        bool saved;
        try
        {
            saved = await memberSkillRepository.TrySaveReviewAsync(memberSkill, cancellationToken);
        }
        catch
        {
            // No row points at the file, so remove it rather than leave an orphan in storage.
            await fileStorageService.DeleteAsync(newPublicId, isPrivate: true, CancellationToken.None);
            throw;
        }

        // The director reviewed the row while the file was uploading; their decision stands on what they saw.
        if (!saved)
        {
            await fileStorageService.DeleteAsync(newPublicId, isPrivate: true, CancellationToken.None);
            return Result<MemberSkillDto>.Failure(ErrorCodes.MemberSkillAlreadyReviewed);
        }

        // The row already points at the new file; an old file the provider failed to delete is only an orphan in storage.
        if (previousPublicId is not null)
        {
            await fileStorageService.DeleteAsync(previousPublicId, isPrivate: true, CancellationToken.None);
        }

        return Result<MemberSkillDto>.Success(ToDto<MemberSkillDto>(memberSkill));
    }

    public async Task<Result<PagedList<MemberSkillDetailDto>>> GetPendingAsync(
        Guid directorUserId, PagingRequest paging, CancellationToken cancellationToken)
    {
        var page = await memberSkillRepository.GetPendingAsync(directorUserId, paging, cancellationToken);

        return Result<PagedList<MemberSkillDetailDto>>.Success(new PagedList<MemberSkillDetailDto>(
            page.Items.Select(ToDto<MemberSkillDetailDto>).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public Task<Result<MemberSkillDetailDto>> ApproveAsync(
        Guid directorUserId, Guid id, CancellationToken cancellationToken) =>
        ReviewAsync(directorUserId, id, ApprovalStatus.Approved, null, cancellationToken);

    public Task<Result<MemberSkillDetailDto>> RejectAsync(
        Guid directorUserId, Guid id, RejectMemberSkillRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(directorUserId, id, ApprovalStatus.Rejected, request.Reason.Trim(), cancellationToken);

    public async Task<Result<MemberSkillDetailDto>> RevokeAsync(
        Guid directorUserId, Guid id, RevokeMemberSkillRequest request, CancellationToken cancellationToken)
    {
        var memberSkill = await memberSkillRepository.GetForReviewAsync(id, cancellationToken);
        if (memberSkill is null
            || !await memberProfileRepository.IsManagedByDirectorAsync(memberSkill.MemberId, directorUserId, cancellationToken))
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillNotFound);
        }

        if (memberSkill.Status != ApprovalStatus.Approved)
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillNotApproved);
        }

        var reason = request.Reason.Trim();
        memberSkill.Status = ApprovalStatus.Revoked;
        memberSkill.RevokedBy = directorUserId;
        memberSkill.RevokedAt = DateTime.UtcNow;
        memberSkill.RevokeReason = reason;
        var affectedEvents = await DropUpcomingLinesAsync(memberSkill.MemberId, memberSkill.SkillId, cancellationToken);

        // Saved together with the dropped lines; another director changed the row first, so nothing is written.
        if (!await memberSkillRepository.TrySaveReviewAsync(memberSkill, cancellationToken))
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillNotApproved);
        }

        var skillName = memberSkill.Skill.Name;
        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.SkillReview, "Kỹ năng bị thu hồi",
                $"Kỹ năng {skillName} của bạn đã bị thu hồi: {reason}",
                [memberSkill.Member.UserId], nameof(MemberSkill), memberSkill.Id),
            cancellationToken);
        await NotifyLeadDirectorsAsync(affectedEvents, directorUserId, skillName, cancellationToken);

        return Result<MemberSkillDetailDto>.Success(ToDto<MemberSkillDetailDto>(memberSkill));
    }

    /// <summary>
    /// Removes the member's lines for the skill on upcoming rosters, reopening each finalized one so its lead director can
    /// staff the gap. Saved with the caller's change; returns the events touched.
    /// </summary>
    private async Task<List<LiturgicalEvent>> DropUpcomingLinesAsync(
        Guid memberId, Guid skillId, CancellationToken cancellationToken)
    {
        var lines = await rosterRepository.GetUpcomingAssignmentsForUpdateAsync(
            memberId, skillId, VietnamTime.Today, cancellationToken);
        foreach (var line in lines)
        {
            var roster = line.Roster;
            roster.Assignments.Remove(line);
            if (roster.Status == RosterStatus.Finalized)
            {
                roster.Status = RosterStatus.Draft;
                roster.FinalizedAt = null;
                roster.FinalizedBy = null;
            }

            if (line.SongListItemId is null)
            {
                roster.WholeServiceFinalizedAt = null;
            }
        }

        return lines.Select(x => x.Roster.LiturgicalEvent).DistinctBy(x => x.Id).ToList();
    }

    private async Task NotifyLeadDirectorsAsync(
        List<LiturgicalEvent> events, Guid revokerUserId, string skillName, CancellationToken cancellationToken)
    {
        foreach (var liturgicalEvent in events)
        {
            if (await choirScope.GetLeadChoirIdAsync(liturgicalEvent.Id, cancellationToken) is not { } leadChoirId
                || await choirRepository.GetByIdAsync(leadChoirId, cancellationToken) is not { DirectorId: { } directorId }
                || directorId == revokerUserId)
            {
                continue;
            }

            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.AssignmentNotice, "Phân công bị gỡ do thu hồi kỹ năng",
                    $"Một ca viên bị thu hồi kỹ năng {skillName} nên đã bị gỡ khỏi phân công sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}; vui lòng xếp người thay.",
                    [directorId], nameof(LiturgicalEvent), liturgicalEvent.Id),
                cancellationToken);
        }
    }

    private async Task<Result<MemberSkillDetailDto>> ReviewAsync(
        Guid directorUserId, Guid id, ApprovalStatus decision, string? rejectReason, CancellationToken cancellationToken)
    {
        var memberSkill = await memberSkillRepository.GetForReviewAsync(id, cancellationToken);
        if (memberSkill is null
            || !await memberProfileRepository.IsManagedByDirectorAsync(memberSkill.MemberId, directorUserId, cancellationToken))
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberSkillNotFound);
        }

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
                decision == ApprovalStatus.Approved ? "Kỹ năng đã được duyệt" : "Kỹ năng bị từ chối",
                decision == ApprovalStatus.Approved
                    ? $"Kỹ năng {skillName} của bạn đã được duyệt."
                    : $"Kỹ năng {skillName} của bạn đã bị từ chối: {rejectReason}",
                [memberSkill.Member.UserId],
                nameof(MemberSkill),
                memberSkill.Id),
            cancellationToken);

        return Result<MemberSkillDetailDto>.Success(ToDto<MemberSkillDetailDto>(memberSkill));
    }

    /// <summary>Extension and size come from the server-side upload; the client's Content-Type is never trusted.</summary>
    private static string? CheckEvidence(FileContent? file)
    {
        if (file is null || file.Length == 0) return ErrorCodes.MemberSkillEvidenceRequired;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (VoiceExtensions.Contains(extension))
        {
            return file.Length > MaxVoiceSizeBytes ? ErrorCodes.MemberSkillEvidenceTooLarge : null;
        }

        if (ClipExtensions.Contains(extension))
        {
            return file.Length > MaxClipSizeBytes ? ErrorCodes.MemberSkillEvidenceTooLarge : null;
        }

        return ErrorCodes.MemberSkillEvidenceTypeNotAllowed;
    }

    // Callers reach here only after their ownership or director-scope check, so signing the URL is safe.
    private TDto ToDto<TDto>(MemberSkill memberSkill) where TDto : MemberSkillDto
    {
        var dto = mapper.Map<TDto>(memberSkill);

        if (memberSkill.EvidencePublicId is { } publicId)
        {
            dto.Evidence = new MemberSkillEvidenceDto
            {
                Type = VoiceExtensions.Contains(Path.GetExtension(publicId).ToLowerInvariant()) ? "Voice" : "Clip",
                FileName = memberSkill.EvidenceFileName!,
                FileSizeBytes = memberSkill.EvidenceSizeBytes!.Value,
                UploadedAt = memberSkill.EvidenceUploadedAt!.Value,
                Url = fileStorageService.GetSignedUrl(publicId),
            };
        }

        return dto;
    }
}

using System.Net;
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
    IGenericRepository<ChoirMembership> membershipRepository,
    IGenericRepository<Choir> choirRepository,
    IChoirJoinRequestRepository joinRequestRepository,
    IUserRepository userRepository,
    INotificationService notificationService,
    IFileStorageService fileStorageService,
    IServiceRosterRepository rosterRepository,
    IChoirScopeService choirScope,
    IEmailSender emailSender,
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

        var memberSkill = new MemberSkill
        {
            Id = Guid.NewGuid(), MemberId = member.Id, SkillId = request.SkillId, Status = ApprovalStatus.Pending,
            DeclaredAt = DateTime.UtcNow,
        };
        if (await AddCheckedAsync(member, memberSkill, cancellationToken) is { } error)
        {
            return Result<MemberSkillDto>.Failure(error);
        }

        var saved = await memberSkillRepository.GetWithSkillAsync(memberSkill.Id, cancellationToken);
        await NotifyReviewersAsync(member, saved!, cancellationToken);
        return Result<MemberSkillDto>.Success(mapper.Map<MemberSkillDto>(saved));
    }

    public async Task<Result<MemberSkillDetailDto>> AssignAsync(
        Guid directorUserId, AssignMemberSkillRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetWithUserAsync(request.MemberId, cancellationToken);
        if (member is null
            || !await memberProfileRepository.IsManagedByDirectorAsync(member.Id, directorUserId, cancellationToken))
        {
            return Result<MemberSkillDetailDto>.Failure(ErrorCodes.MemberNotFound);
        }

        // The director heard the member themselves, so the row skips the Pending step.
        var now = DateTime.UtcNow;
        var memberSkill = new MemberSkill
        {
            Id = Guid.NewGuid(), MemberId = member.Id, SkillId = request.SkillId, Status = ApprovalStatus.Approved,
            DeclaredAt = now, ApprovedBy = directorUserId, ApprovedAt = now,
        };
        if (await AddCheckedAsync(member, memberSkill, cancellationToken) is { } error)
        {
            return Result<MemberSkillDetailDto>.Failure(error);
        }

        var saved = await memberSkillRepository.GetForReviewAsync(memberSkill.Id, cancellationToken);
        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.SkillReview, "Kỹ năng được ca trưởng ghi nhận",
                $"Ca trưởng đã ghi nhận kỹ năng {saved!.Skill.Name} cho bạn.",
                [member.UserId], nameof(MemberSkill), memberSkill.Id),
            cancellationToken);

        return Result<MemberSkillDetailDto>.Success(ToDto<MemberSkillDetailDto>(saved));
    }

    public async Task<Result<ApproveMemberSkillsResponse>> ApproveManyAsync(
        Guid directorUserId, ApproveMemberSkillsRequest request, CancellationToken cancellationToken)
    {
        var response = new ApproveMemberSkillsResponse();

        // One review per row, as the single approve does: a row that fails does not hold back the others.
        foreach (var id in request.Ids.Distinct())
        {
            var result = await ReviewAsync(directorUserId, id, ApprovalStatus.Approved, null, cancellationToken);
            if (result.IsSuccess)
            {
                response.Approved.Add(result.Value!);
            }
            else
            {
                response.Failed.Add(new MemberSkillReviewFailureDto { Id = id, Code = result.Code! });
            }
        }

        return Result<ApproveMemberSkillsResponse>.Success(response);
    }

    /// <summary>
    /// Checks the member and the skill for a new row and saves it. Returns the error code, or null once saved. A skill
    /// rejected or revoked before may be added again; a Pending or Approved one may not.
    /// </summary>
    private async Task<string?> AddCheckedAsync(
        MemberProfile member, MemberSkill memberSkill, CancellationToken cancellationToken)
    {
        if (member.Status != MemberStatus.Active) return ErrorCodes.MemberNotActive;

        var skill = await memberSkillRepository.GetSkillWithCategoryAsync(memberSkill.SkillId, cancellationToken);
        if (skill is null) return ErrorCodes.SkillNotFound;

        // A skill in a disabled category is hidden from the lookup, so it counts as inactive too.
        if (!skill.IsActive || !skill.Category.IsActive) return ErrorCodes.SkillInactive;

        if (skill.Gender is { } skillGender)
        {
            var memberGender = member.User.Gender;
            if (memberGender is null) return ErrorCodes.MemberSkillGenderRequired;
            if (memberGender != skillGender) return ErrorCodes.MemberSkillGenderMismatch;
        }

        if (await memberSkillRepository.HasActiveDeclarationAsync(member.Id, skill.Id, cancellationToken)
            || !await memberSkillRepository.TryAddAsync(memberSkill, cancellationToken))
        {
            return ErrorCodes.MemberSkillAlreadyDeclared;
        }

        return null;
    }

    /// <summary>
    /// Tells who reviews the new declaration (D9): the director of the member's choir; with no choir, the director of
    /// the choir they asked to join; otherwise every active Choir Director, since any of them may review a member with
    /// no choir.
    /// </summary>
    private async Task NotifyReviewersAsync(MemberProfile member, MemberSkill memberSkill, CancellationToken cancellationToken)
    {
        var memberId = member.Id;
        var choirIds = (await membershipRepository.ListAsync(x => x.MemberId == memberId && x.IsActive, cancellationToken))
            .Select(x => x.ChoirId)
            .ToList();
        if (choirIds.Count == 0)
        {
            choirIds = (await joinRequestRepository.ListAsync(
                    x => x.MemberId == memberId && x.Status == ChoirJoinRequestStatus.Pending && !x.IsInvitation,
                    cancellationToken))
                .Select(x => x.ChoirId)
                .ToList();
        }

        var directorIds = choirIds.Count == 0
            ? []
            : (await choirRepository.ListAsync(x => choirIds.Contains(x.Id) && x.DirectorId != null, cancellationToken))
                .Select(x => x.DirectorId!.Value)
                .Distinct()
                .ToList();
        if (directorIds.Count == 0)
        {
            directorIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ChoirDirector], cancellationToken);
        }

        if (directorIds.Count == 0)
        {
            return;
        }

        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.SkillReview, "Kỹ năng chờ duyệt",
                $"{member.User.FullName} khai báo kỹ năng {memberSkill.Skill.Name}, vui lòng xem và duyệt.",
                directorIds, nameof(MemberSkill), memberSkill.Id),
            cancellationToken);
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
        await EmailMemberAsync(
            memberSkill.Member.User, "Harmonia - Kỹ năng bị thu hồi",
            $"Kỹ năng {WebUtility.HtmlEncode(skillName)} của bạn đã bị ca trưởng thu hồi.</p>"
                + $"<p>Lý do: {WebUtility.HtmlEncode(reason)}",
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
            var directorIds = (await choirScope.GetLeadDirectorUserIdsAsync(liturgicalEvent.Id, cancellationToken))
                .Where(x => x != revokerUserId)
                .ToList();
            if (directorIds.Count == 0)
            {
                continue;
            }

            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.AssignmentNotice, "Phân công bị gỡ do thu hồi kỹ năng",
                    $"Một ca viên bị thu hồi kỹ năng {skillName} nên đã bị gỡ khỏi phân công sự kiện ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}; vui lòng xếp người thay.",
                    directorIds, nameof(LiturgicalEvent), liturgicalEvent.Id),
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
        await EmailMemberAsync(
            memberSkill.Member.User, "Harmonia - Kết quả duyệt kỹ năng",
            decision == ApprovalStatus.Approved
                ? $"Kỹ năng {WebUtility.HtmlEncode(skillName)} bạn khai báo đã được ca trưởng duyệt."
                : $"Kỹ năng {WebUtility.HtmlEncode(skillName)} bạn khai báo chưa được ca trưởng duyệt.</p>"
                    + $"<p>Lý do: {WebUtility.HtmlEncode(rejectReason)}",
            cancellationToken);

        return Result<MemberSkillDetailDto>.Success(ToDto<MemberSkillDetailDto>(memberSkill));
    }

    /// <summary>
    /// The result is ignored on purpose: the review is already saved, the in-app notification went out,
    /// and the sender logs the failure. <paramref name="htmlMessage"/> must already be HTML-encoded.
    /// </summary>
    private Task EmailMemberAsync(User user, string subject, string htmlMessage, CancellationToken cancellationToken) =>
        emailSender.SendAsync(
            user.Email, subject,
            $"<p>Chào {WebUtility.HtmlEncode(user.FullName)},</p><p>{htmlMessage}</p>",
            cancellationToken);

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

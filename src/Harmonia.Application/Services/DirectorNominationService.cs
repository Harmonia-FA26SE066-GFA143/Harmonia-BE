using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class DirectorNominationService(
    IDirectorNominationRepository nominationRepository,
    IMemberProfileRepository memberRepository,
    IGenericRepository<Choir> choirRepository,
    IUserRepository userRepository,
    IChoirScopeService choirScope,
    IUserService userService,
    IChoirService choirService,
    ISystemSettingService systemSettingService,
    INotificationService notificationService,
    ICurrentUserService currentUser,
    IMapper mapper) : IDirectorNominationService
{
    public async Task<Result<DirectorNominationStatsDto>> GetEligibilityAsync(
        Guid memberId, CancellationToken cancellationToken)
    {
        var (_, error) = await GetNominatableMemberAsync(memberId, cancellationToken);
        return error is not null
            ? Result<DirectorNominationStatsDto>.Failure(error)
            : Result<DirectorNominationStatsDto>.Success(await GetStatsAsync(memberId, cancellationToken));
    }

    public async Task<Result<DirectorNominationDetailDto>> CreateAsync(
        CreateDirectorNominationRequest request, CancellationToken cancellationToken)
    {
        var (member, error) = await GetNominatableMemberAsync(request.MemberId, cancellationToken);
        if (error is not null)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        if (await nominationRepository.HasOpenAsync(request.MemberId, cancellationToken))
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.DirectorNominationAlreadyOpen);
        }

        if (request.ProposedChoirId is { } choirId
            && await choirRepository.GetByIdAsync(choirId, cancellationToken) is null)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.ChoirNotFound);
        }

        var nomination = new DirectorNomination
        {
            Id = Guid.NewGuid(),
            MemberId = request.MemberId,
            NominatedById = currentUser.UserId!.Value,
            Reason = request.Reason.Trim(),
            ProposedChoirId = request.ProposedChoirId,
            Status = DirectorNominationStatus.Pending,
        };
        await nominationRepository.AddAsync(nomination, cancellationToken);
        await nominationRepository.SaveChangesAsync(cancellationToken);

        var detail = await ToDetailAsync(nomination.Id, cancellationToken);
        if (currentUser.RoleName == RoleNames.ChoirDirector)
        {
            var priestIds = await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.ParishPriest], cancellationToken);
            await NotifyAsync(detail, "Đề cử ca trưởng mới",
                $"{detail.NominatedByFullName} đề cử {member!.User.FullName} làm ca trưởng.", priestIds, cancellationToken);
        }

        return Result<DirectorNominationDetailDto>.Success(detail);
    }

    public async Task<Result<PagedList<DirectorNominationDto>>> SearchAsync(
        SearchDirectorNominationsRequest request, CancellationToken cancellationToken)
    {
        var (nominatedById, memberUserId) = VisibilityFilter();
        var page = await nominationRepository.SearchAsync(request, nominatedById, memberUserId, cancellationToken);

        return Result<PagedList<DirectorNominationDto>>.Success(new PagedList<DirectorNominationDto>(
            mapper.Map<List<DirectorNominationDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<DirectorNominationDetailDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var (nominatedById, memberUserId) = VisibilityFilter();
        var nomination = await nominationRepository.GetWithDetailsAsync(id, nominatedById, memberUserId, cancellationToken);

        return nomination is null
            ? Result<DirectorNominationDetailDto>.Failure(ErrorCodes.DirectorNominationNotFound)
            : Result<DirectorNominationDetailDto>.Success(await ToDetailAsync(nomination, cancellationToken));
    }

    public async Task<Result<DirectorNominationDetailDto>> WithdrawAsync(Guid id, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (nomination is null
            || (currentUser.RoleName == RoleNames.ChoirDirector && nomination.NominatedById != currentUser.UserId))
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.DirectorNominationNotFound);
        }

        // A Parish Priest sees every nomination but only takes back their own; another's is rejected instead.
        if (nomination.NominatedById != currentUser.UserId)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.Forbidden);
        }

        if (nomination.Status != DirectorNominationStatus.Pending)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.DirectorNominationInvalidStatus);
        }

        nomination.Status = DirectorNominationStatus.Cancelled;
        await nominationRepository.SaveChangesAsync(cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(await ToDetailAsync(id, cancellationToken));
    }

    public async Task<Result<DirectorNominationDetailDto>> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (CheckStatus(nomination, DirectorNominationStatus.Pending) is { } error)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        nomination!.Status = DirectorNominationStatus.Approved;
        nomination.ReviewedById = currentUser.UserId;
        nomination.ReviewedAt = DateTime.UtcNow;
        await nominationRepository.SaveChangesAsync(cancellationToken);

        var detail = await ToDetailAsync(id, cancellationToken);
        await NotifyAsync(detail, "Đề cử ca trưởng đã được duyệt",
            $"Cha xứ đã duyệt đề cử {detail.MemberFullName} làm ca trưởng.", [detail.NominatedById], cancellationToken);
        await NotifyAsync(detail, "Bạn được đề cử làm ca trưởng",
            "Cha xứ đã duyệt đề cử bạn làm ca trưởng. Bước tiếp theo là đào tạo hoặc được miễn đào tạo.",
            [detail.MemberUserId], cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(detail);
    }

    public async Task<Result<DirectorNominationDetailDto>> RejectAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (CheckStatus(nomination, DirectorNominationStatus.Pending) is { } error)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        nomination!.Status = DirectorNominationStatus.Rejected;
        nomination.ReviewedById = currentUser.UserId;
        nomination.ReviewedAt = DateTime.UtcNow;
        nomination.ReviewNote = request.Note.Trim();
        await nominationRepository.SaveChangesAsync(cancellationToken);

        var detail = await ToDetailAsync(id, cancellationToken);
        await NotifyAsync(detail, "Đề cử ca trưởng bị từ chối",
            $"Đề cử {detail.MemberFullName} làm ca trưởng bị từ chối: {detail.ReviewNote}",
            [detail.NominatedById], cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(detail);
    }

    public async Task<Result<DirectorNominationDetailDto>> CancelAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (CheckStatus(nomination, DirectorNominationStatus.Approved) is { } error)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        nomination!.Status = DirectorNominationStatus.Cancelled;
        nomination.ReviewNote = request.Note.Trim();
        await nominationRepository.SaveChangesAsync(cancellationToken);

        var detail = await ToDetailAsync(id, cancellationToken);
        await NotifyAsync(detail, "Đề cử ca trưởng đã bị huỷ",
            $"Đề cử {detail.MemberFullName} làm ca trưởng đã bị huỷ: {detail.ReviewNote}",
            [detail.NominatedById, detail.MemberUserId], cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(detail);
    }

    public async Task<Result<DirectorNominationDetailDto>> WaiveTrainingAsync(
        Guid id, ReviewDirectorNominationRequest request, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (CheckStatus(nomination, DirectorNominationStatus.Approved) is { } error)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        nomination!.Status = DirectorNominationStatus.Trained;
        nomination.TrainingWaiverReason = request.Note.Trim();
        await nominationRepository.SaveChangesAsync(cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(await ToDetailAsync(id, cancellationToken));
    }

    public async Task<Result<DirectorNominationDetailDto>> AppointAsync(
        Guid id, AppointDirectorNominationRequest request, CancellationToken cancellationToken)
    {
        var nomination = await nominationRepository.GetByIdAsync(id, cancellationToken);
        if (CheckStatus(nomination, DirectorNominationStatus.Trained) is { } error)
        {
            return Result<DirectorNominationDetailDto>.Failure(error);
        }

        var choir = await choirRepository.GetByIdAsync(request.ChoirId, cancellationToken);
        if (choir is null)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.ChoirNotFound);
        }

        if (!choir.IsActive)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.ChoirInactive);
        }

        var member = await memberRepository.GetByIdAsync(nomination!.MemberId, cancellationToken);
        if (member is null)
        {
            return Result<DirectorNominationDetailDto>.Failure(ErrorCodes.MemberNotFound);
        }

        // Reuse the two existing Admin actions so their rules (token revocation, leaving choirs, director checks)
        // apply exactly as when the Admin does them by hand.
        var roleResult = await userService.AssignRoleAsync(
            member.UserId, new AssignRoleRequest { RoleName = RoleNames.ChoirDirector }, cancellationToken);
        if (!roleResult.IsSuccess)
        {
            return Result<DirectorNominationDetailDto>.Failure(roleResult.Code!, roleResult.Message);
        }

        var choirResult = await choirService.UpdateAsync(
            choir.Id,
            new UpdateChoirRequest
            {
                Name = choir.Name, Description = choir.Description, IsActive = choir.IsActive, DirectorId = member.UserId,
            },
            cancellationToken);
        if (!choirResult.IsSuccess)
        {
            return Result<DirectorNominationDetailDto>.Failure(choirResult.Code!, choirResult.Message);
        }

        nomination.Status = DirectorNominationStatus.Appointed;
        nomination.AppointedChoirId = choir.Id;
        nomination.AppointedAt = DateTime.UtcNow;
        await nominationRepository.SaveChangesAsync(cancellationToken);

        var detail = await ToDetailAsync(id, cancellationToken);
        await NotifyAsync(detail, "Bạn đã được bổ nhiệm ca trưởng",
            $"Bạn được bổ nhiệm làm ca trưởng ca đoàn {choir.Name}. Vui lòng đăng nhập lại để dùng chức năng ca trưởng.",
            [detail.MemberUserId], cancellationToken);
        await NotifyAsync(detail, "Ca trưởng mới đã được bổ nhiệm",
            $"{detail.MemberFullName} đã được bổ nhiệm làm ca trưởng ca đoàn {choir.Name}.",
            [detail.NominatedById], cancellationToken);

        return Result<DirectorNominationDetailDto>.Success(detail);
    }

    /// <summary>
    /// The member must exist, be in the calling director's choirs (others look missing, D8) and still be an active
    /// Choir Member: a role change away from Choir Member leaves the profile Inactive.
    /// </summary>
    private async Task<(MemberProfile? Member, string? Error)> GetNominatableMemberAsync(
        Guid memberId, CancellationToken cancellationToken)
    {
        var member = await memberRepository.GetWithUserAsync(memberId, cancellationToken);
        if (member is null)
        {
            return (null, ErrorCodes.MemberNotFound);
        }

        if (await choirScope.GetDirectedChoirIdsAsync(cancellationToken) is { } directedChoirIds)
        {
            var memberChoirIds = await choirScope.GetMemberChoirIdsAsync(memberId, cancellationToken);
            if (!memberChoirIds.Intersect(directedChoirIds).Any())
            {
                return (null, ErrorCodes.MemberNotFound);
            }
        }

        return member.Status != MemberStatus.Active || !member.User.IsActive
            ? (member, ErrorCodes.MemberNotActive)
            : (member, null);
    }

    private (Guid? NominatedById, Guid? MemberUserId) VisibilityFilter() => currentUser.RoleName switch
    {
        RoleNames.ChoirDirector => (currentUser.UserId, null),
        RoleNames.ChoirMember => (null, currentUser.UserId),
        _ => (null, null),
    };

    private static string? CheckStatus(DirectorNomination? nomination, DirectorNominationStatus expected) =>
        nomination is null ? ErrorCodes.DirectorNominationNotFound
        : nomination.Status != expected ? ErrorCodes.DirectorNominationInvalidStatus
        : null;

    private async Task<DirectorNominationDetailDto> ToDetailAsync(Guid id, CancellationToken cancellationToken) =>
        await ToDetailAsync(
            (await nominationRepository.GetWithDetailsAsync(id, null, null, cancellationToken))!, cancellationToken);

    private async Task<DirectorNominationDetailDto> ToDetailAsync(
        DirectorNomination nomination, CancellationToken cancellationToken)
    {
        var detail = mapper.Map<DirectorNominationDetailDto>(nomination);
        detail.Stats = await GetStatsAsync(nomination.MemberId, cancellationToken);
        return detail;
    }

    private async Task<DirectorNominationStatsDto> GetStatsAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var today = VietnamTime.Today;
        var raw = await nominationRepository.GetMemberStatsAsync(
            memberId, VietnamTime.StartOfDayUtc(today.AddMonths(-12)), today, cancellationToken);

        var minTenure = await systemSettingService.GetIntAsync(SystemSettingKeys.NominationMinTenureMonths, cancellationToken);
        var minAttendance = await systemSettingService.GetIntAsync(
            SystemSettingKeys.NominationMinAttendancePercent, cancellationToken);
        var minServed = await systemSettingService.GetIntAsync(SystemSettingKeys.NominationMinServedEvents, cancellationToken);
        var skillRequired = await systemSettingService.GetBoolAsync(
            SystemSettingKeys.NominationRequireConductingSkill, cancellationToken);

        var tenure = raw.FirstJoinedDate is { } joined ? MonthsBetween(joined, today) : 0;
        int? attendancePercent = raw.AttendanceRecorded == 0
            ? null
            : raw.AttendancePresent * 100 / raw.AttendanceRecorded;

        var stats = new DirectorNominationStatsDto
        {
            FirstJoinedDate = raw.FirstJoinedDate,
            TenureMonths = tenure,
            MinTenureMonths = minTenure,
            MeetsTenure = tenure >= minTenure,
            AttendanceRecorded = raw.AttendanceRecorded,
            AttendancePresent = raw.AttendancePresent,
            AttendancePercent = attendancePercent,
            MinAttendancePercent = minAttendance,
            MeetsAttendance = (attendancePercent ?? 0) >= minAttendance,
            ServedEventCount = raw.ServedEventCount,
            MinServedEvents = minServed,
            MeetsServedEvents = raw.ServedEventCount >= minServed,
            HasConductingSkill = raw.HasConductingSkill,
            IsConductingSkillRequired = skillRequired,
            MeetsConductingSkill = raw.HasConductingSkill || !skillRequired,
        };
        stats.MeetsAll = stats.MeetsTenure && stats.MeetsAttendance && stats.MeetsServedEvents && stats.MeetsConductingSkill;
        return stats;
    }

    /// <summary>Whole months from <paramref name="from"/> to <paramref name="to"/>, never negative.</summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month - (to.Day < from.Day ? 1 : 0);
        return Math.Max(months, 0);
    }

    /// <summary>Tells the recipients about the nomination, never the caller about their own action.</summary>
    private Task NotifyAsync(
        DirectorNominationDetailDto nomination, string title, string content, IEnumerable<Guid> recipientIds,
        CancellationToken cancellationToken)
    {
        var recipients = recipientIds.Where(x => x != currentUser.UserId).Distinct().ToList();
        return recipients.Count == 0
            ? Task.CompletedTask
            : notificationService.SendAsync(
                new SendNotificationRequest(NotificationType.DirectorNomination, title, content, recipients,
                    nameof(DirectorNomination), nomination.Id),
                cancellationToken);
    }
}

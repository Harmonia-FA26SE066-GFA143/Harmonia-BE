using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ChoirJoinRequestService(
    IChoirJoinRequestRepository joinRequestRepository,
    IGenericRepository<Choir> choirRepository,
    IGenericRepository<ChoirMembership> membershipRepository,
    IMemberProfileRepository memberRepository,
    IUserRepository userRepository,
    IChoirService choirService,
    INotificationService notificationService,
    ICurrentUserService currentUser,
    IMapper mapper) : IChoirJoinRequestService
{
    public async Task<Result<ChoirJoinRequestDto>> CreateAsync(
        CreateChoirJoinRequestRequest request, CancellationToken cancellationToken)
    {
        var member = await GetCallerMemberAsync(cancellationToken);
        if (member is null || member.Status != MemberStatus.Active)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.MemberNotFound);
        }

        var choir = await choirRepository.GetByIdAsync(request.ChoirId, cancellationToken);
        if (choir is null)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.ChoirNotFound);
        }

        if (!choir.IsActive)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.ChoirInactive);
        }

        var memberId = member.Id;
        var choirId = choir.Id;
        if ((await membershipRepository.ListAsync(
                x => x.MemberId == memberId && x.ChoirId == choirId && x.IsActive, cancellationToken)).Count > 0)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.ChoirJoinRequestAlreadyMember);
        }

        // One own request at a time; an invitation from this choir is answered, not asked past.
        if ((await joinRequestRepository.ListAsync(
                x => x.MemberId == memberId && x.Status == ChoirJoinRequestStatus.Pending
                    && (!x.IsInvitation || x.ChoirId == choirId),
                cancellationToken)).Count > 0)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.ChoirJoinRequestAlreadyPending);
        }

        var row = new ChoirJoinRequest
        {
            Id = Guid.NewGuid(), MemberId = memberId, ChoirId = choirId, Status = ChoirJoinRequestStatus.Pending,
            Message = TrimToNull(request.Message),
        };
        await joinRequestRepository.AddAsync(row, cancellationToken);
        await joinRequestRepository.SaveChangesAsync(cancellationToken);

        var dto = await ToDtoAsync(row.Id, cancellationToken);
        // A choir with no director yet is looked after by the Admin.
        var recipients = choir.DirectorId is { } directorId
            ? [directorId]
            : await userRepository.GetActiveUserIdsByRolesAsync([RoleNames.Admin], cancellationToken);
        var (title, content) = dto.CurrentChoirName is { } currentChoirName
            ? ("Yêu cầu chuyển ca đoàn",
                $"{dto.MemberFullName} xin chuyển từ ca đoàn {currentChoirName} sang ca đoàn {choir.Name}.")
            : ("Yêu cầu vào ca đoàn", $"{dto.MemberFullName} xin vào ca đoàn {choir.Name}.");
        await NotifyAsync(recipients.Cast<Guid?>(), title, content, row.Id, cancellationToken);

        return Result<ChoirJoinRequestDto>.Success(dto);
    }

    public async Task<Result<PagedList<ChoirJoinRequestDto>>> SearchAsync(
        SearchChoirJoinRequestsRequest request, CancellationToken cancellationToken)
    {
        Guid? memberId = null;
        Guid? directorUserId = null;
        if (currentUser.RoleName == RoleNames.ChoirMember)
        {
            var member = await GetCallerMemberAsync(cancellationToken);
            if (member is null)
            {
                return Result<PagedList<ChoirJoinRequestDto>>.Success(
                    new PagedList<ChoirJoinRequestDto>([], request.PageNumber, request.PageSize, 0));
            }

            memberId = member.Id;
        }
        else if (currentUser.RoleName == RoleNames.ChoirDirector)
        {
            directorUserId = currentUser.UserId;
        }

        var page = await joinRequestRepository.SearchAsync(request, memberId, directorUserId, cancellationToken);

        return Result<PagedList<ChoirJoinRequestDto>>.Success(new PagedList<ChoirJoinRequestDto>(
            mapper.Map<List<ChoirJoinRequestDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<ChoirJoinRequestDto>> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var (row, error) = await GetPendingAsync(id, IsResponder, cancellationToken);
        if (error is not null)
        {
            return Result<ChoirJoinRequestDto>.Failure(error);
        }

        foreach (var pending in await joinRequestRepository.GetPendingForUpdateAsync([row!.MemberId], cancellationToken))
        {
            if (pending.Id == id)
            {
                pending.Status = ChoirJoinRequestStatus.Approved;
                pending.ReviewedById = currentUser.UserId;
                pending.ReviewedAt = DateTime.UtcNow;
            }
            else
            {
                pending.Status = ChoirJoinRequestStatus.Cancelled;
            }
        }

        // Saves the status changes above together with the move.
        var moved = await choirService.MoveMemberAsync(row.MemberId, row.ChoirId, cancellationToken);
        if (!moved.IsSuccess)
        {
            return Result<ChoirJoinRequestDto>.Failure(moved.Code!);
        }

        if (row.IsInvitation)
        {
            await NotifyAsync(
                [row.Choir.DirectorId], "Ca viên nhận lời mời",
                $"{row.Member.User.FullName} đã nhận lời mời và vào ca đoàn {row.Choir.Name}.", id, cancellationToken);
        }
        else
        {
            await NotifyAsync(
                [row.Member.UserId], "Yêu cầu vào ca đoàn được duyệt", $"Bạn đã vào ca đoàn {row.Choir.Name}.", id,
                cancellationToken);
        }

        return Result<ChoirJoinRequestDto>.Success(await ToDtoAsync(id, cancellationToken));
    }

    public async Task<Result<ChoirJoinRequestDto>> RejectAsync(
        Guid id, RejectChoirJoinRequestRequest request, CancellationToken cancellationToken)
    {
        var (row, error) = await GetPendingAsync(id, IsResponder, cancellationToken);
        if (error is not null)
        {
            return Result<ChoirJoinRequestDto>.Failure(error);
        }

        var reason = TrimToNull(request.Reason);
        if (!row!.IsInvitation && reason is null)
        {
            return Result<ChoirJoinRequestDto>.Failure(ErrorCodes.ChoirJoinRequestRejectReasonRequired);
        }

        var tracked = await joinRequestRepository.GetByIdAsync(id, cancellationToken);
        tracked!.Status = ChoirJoinRequestStatus.Rejected;
        tracked.RejectReason = reason;
        tracked.ReviewedById = currentUser.UserId;
        tracked.ReviewedAt = DateTime.UtcNow;
        await joinRequestRepository.SaveChangesAsync(cancellationToken);

        if (row.IsInvitation)
        {
            await NotifyAsync(
                [row.Choir.DirectorId], "Ca viên từ chối lời mời",
                WithReason($"{row.Member.User.FullName} đã từ chối lời mời vào ca đoàn {row.Choir.Name}.", reason), id,
                cancellationToken);
        }
        else
        {
            await NotifyAsync(
                [row.Member.UserId], "Yêu cầu vào ca đoàn bị từ chối",
                WithReason($"Ca đoàn {row.Choir.Name} chưa nhận yêu cầu của bạn.", reason), id, cancellationToken);
        }

        return Result<ChoirJoinRequestDto>.Success(await ToDtoAsync(id, cancellationToken));
    }

    public async Task<Result<ChoirJoinRequestDto>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var (_, error) = await GetPendingAsync(id, IsCreator, cancellationToken);
        if (error is not null)
        {
            return Result<ChoirJoinRequestDto>.Failure(error);
        }

        var tracked = await joinRequestRepository.GetByIdAsync(id, cancellationToken);
        tracked!.Status = ChoirJoinRequestStatus.Cancelled;
        await joinRequestRepository.SaveChangesAsync(cancellationToken);

        return Result<ChoirJoinRequestDto>.Success(await ToDtoAsync(id, cancellationToken));
    }

    /// <summary>
    /// The row with its details when <paramref name="mayAct"/> lets the caller act on it and it is still Pending.
    /// A row the caller may not act on reads as not found.
    /// </summary>
    private async Task<(ChoirJoinRequest? Row, string? Error)> GetPendingAsync(
        Guid id, Func<ChoirJoinRequest, bool> mayAct, CancellationToken cancellationToken)
    {
        var row = await joinRequestRepository.GetWithDetailsAsync(id, cancellationToken);
        if (row is null || !mayAct(row))
        {
            return (null, ErrorCodes.ChoirJoinRequestNotFound);
        }

        return row.Status == ChoirJoinRequestStatus.Pending ? (row, null) : (null, ErrorCodes.ChoirJoinRequestNotPending);
    }

    /// <summary>The member answers an invitation; the choir's director or the Admin answers a member's request.</summary>
    private bool IsResponder(ChoirJoinRequest row) => row.IsInvitation ? IsTheMember(row) : ManagesChoir(row);

    private bool IsCreator(ChoirJoinRequest row) => row.IsInvitation ? ManagesChoir(row) : IsTheMember(row);

    private bool IsTheMember(ChoirJoinRequest row) => row.Member.UserId == currentUser.UserId;

    private bool ManagesChoir(ChoirJoinRequest row) =>
        currentUser.RoleName == RoleNames.Admin
        || (currentUser.RoleName == RoleNames.ChoirDirector && row.Choir.DirectorId == currentUser.UserId);

    private async Task<MemberProfile?> GetCallerMemberAsync(CancellationToken cancellationToken) =>
        currentUser.UserId is { } userId ? await memberRepository.GetByUserIdAsync(userId, cancellationToken) : null;

    private async Task<ChoirJoinRequestDto> ToDtoAsync(Guid id, CancellationToken cancellationToken) =>
        mapper.Map<ChoirJoinRequestDto>(await joinRequestRepository.GetWithDetailsAsync(id, cancellationToken));

    private async Task NotifyAsync(
        IEnumerable<Guid?> userIds, string title, string content, Guid requestId, CancellationToken cancellationToken)
    {
        var recipients = userIds.OfType<Guid>().Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.ChoirChanged, title, content, recipients, nameof(ChoirJoinRequest), requestId),
            cancellationToken);
    }

    private static string WithReason(string content, string? reason) =>
        reason is null ? content : $"{content} Lý do: {reason}";

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

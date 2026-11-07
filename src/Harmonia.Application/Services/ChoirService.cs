using System.Globalization;
using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class ChoirService(
    IGenericRepository<Choir> choirRepository,
    IGenericRepository<ChoirMembership> membershipRepository,
    IGenericRepository<LiturgicalEventChoir> eventChoirRepository,
    IMemberProfileRepository memberRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IUserRepository userRepository,
    IEventParticipationRepository participationRepository,
    IServiceRosterRepository rosterRepository,
    IGenericRepository<SongList> songListRepository,
    INotificationService notificationService,
    ICurrentUserService currentUser,
    IMapper mapper) : IChoirService
{
    public async Task<Result<ChoirDto>> CreateAsync(CreateChoirRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await IsNameTakenAsync(name, null, cancellationToken))
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirNameAlreadyExists);
        }

        if (!await IsValidDirectorAsync(request.DirectorId, cancellationToken))
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirDirectorInvalid);
        }

        var choir = new Choir
        {
            Id = Guid.NewGuid(), Name = name, Description = TrimToNull(request.Description), DirectorId = request.DirectorId,
        };
        await choirRepository.AddAsync(choir, cancellationToken);
        await choirRepository.SaveChangesAsync(cancellationToken);

        return Result<ChoirDto>.Success((await ToDtosAsync([choir], cancellationToken))[0]);
    }

    public async Task<Result<List<ChoirDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var choirs = currentUser.RoleName is RoleNames.Admin or RoleNames.ParishPriest
            ? await choirRepository.ListAsync(_ => true, cancellationToken)
            : await choirRepository.ListAsync(x => x.DirectorId == userId, cancellationToken);

        return Result<List<ChoirDto>>.Success(await ToDtosAsync(choirs, cancellationToken));
    }

    public async Task<Result<ChoirDto>> UpdateAsync(
        Guid id, UpdateChoirRequest request, CancellationToken cancellationToken)
    {
        var choir = await choirRepository.GetByIdAsync(id, cancellationToken);
        if (choir is null)
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirNotFound);
        }

        var name = request.Name.Trim();
        if (await IsNameTakenAsync(name, id, cancellationToken))
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirNameAlreadyExists);
        }

        // Re-saving the current director is allowed even if that account was deactivated since.
        if (request.DirectorId != choir.DirectorId && !await IsValidDirectorAsync(request.DirectorId, cancellationToken))
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirDirectorInvalid);
        }

        choir.Name = name;
        choir.Description = TrimToNull(request.Description);
        choir.IsActive = request.IsActive;
        choir.DirectorId = request.DirectorId;
        await choirRepository.SaveChangesAsync(cancellationToken);

        return Result<ChoirDto>.Success((await ToDtosAsync([choir], cancellationToken))[0]);
    }

    public async Task<Result<List<ChoirMemberDto>>> GetMembersAsync(Guid choirId, CancellationToken cancellationToken)
    {
        if (await GetManagedChoirAsync(choirId, cancellationToken) is null)
        {
            return Result<List<ChoirMemberDto>>.Failure(ErrorCodes.ChoirNotFound);
        }

        var memberships = await membershipRepository.ListAsync(
            x => x.ChoirId == choirId && x.IsActive, cancellationToken);
        var members = (await memberRepository.GetByIdsWithUserAsync(
                memberships.Select(x => x.MemberId).ToList(), cancellationToken))
            .ToDictionary(x => x.Id);

        var result = memberships
            .Where(x => members.ContainsKey(x.MemberId))
            .Select(x =>
            {
                var user = members[x.MemberId].User;
                return new ChoirMemberDto
                {
                    MemberId = x.MemberId, UserId = user.Id, FullName = user.FullName, Email = user.Email,
                    JoinedDate = x.JoinedDate,
                };
            })
            .OrderBy(x => x.FullName)
            .ToList();

        return Result<List<ChoirMemberDto>>.Success(result);
    }

    public async Task<Result> AddMembersAsync(
        Guid choirId, AddChoirMembersRequest request, CancellationToken cancellationToken)
    {
        var choir = await GetManagedChoirAsync(choirId, cancellationToken);
        if (choir is null)
        {
            return Result.Failure(ErrorCodes.ChoirNotFound);
        }

        if (!choir.IsActive)
        {
            return Result.Failure(ErrorCodes.ChoirInactive);
        }

        var memberIds = request.MemberIds.Distinct().ToList();
        var activeMembers = await memberRepository.ListAsync(
            x => memberIds.Contains(x.Id) && x.Status == MemberStatus.Active, cancellationToken);
        if (activeMembers.Count != memberIds.Count)
        {
            return Result.Failure(ErrorCodes.ChoirMemberInvalid);
        }

        // Read-only rows; the unique (MemberId, ChoirId) index means a member who left rejoins on the old row.
        var existing = (await membershipRepository.ListAsync(
                x => x.ChoirId == choirId && memberIds.Contains(x.MemberId), cancellationToken))
            .ToDictionary(x => x.MemberId);
        var today = VietnamTime.Today;

        foreach (var memberId in memberIds)
        {
            if (!existing.TryGetValue(memberId, out var row))
            {
                await membershipRepository.AddAsync(new ChoirMembership
                {
                    Id = Guid.NewGuid(), ChoirId = choirId, MemberId = memberId, JoinedDate = today,
                }, cancellationToken);
            }
            else if (!row.IsActive)
            {
                var tracked = await membershipRepository.GetByIdAsync(row.Id, cancellationToken);
                tracked!.IsActive = true;
                tracked.JoinedDate = today;
            }
        }

        await membershipRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid choirId, Guid memberId, CancellationToken cancellationToken)
    {
        if (await GetManagedChoirAsync(choirId, cancellationToken) is null)
        {
            return Result.Failure(ErrorCodes.ChoirNotFound);
        }

        var row = (await membershipRepository.ListAsync(
                x => x.ChoirId == choirId && x.MemberId == memberId && x.IsActive, cancellationToken))
            .SingleOrDefault();
        if (row is null)
        {
            return Result.Failure(ErrorCodes.ChoirMembershipNotFound);
        }

        var tracked = await membershipRepository.GetByIdAsync(row.Id, cancellationToken);
        tracked!.IsActive = false;
        await membershipRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<List<LiturgicalEventChoirDto>>> GetEventChoirsAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        if (await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken) is null)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        return Result<List<LiturgicalEventChoirDto>>.Success(await LoadEventChoirsAsync(eventId, cancellationToken));
    }

    public async Task<Result<List<LiturgicalEventChoirDto>>> UpdateEventChoirsAsync(
        Guid eventId, UpdateEventChoirsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (EditableEventError(liturgicalEvent) is { } eventError)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(eventError);
        }

        var choirIds = request.ChoirIds.Distinct().ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);
        if (choirs.Count != choirIds.Count)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.ChoirNotFound);
        }

        var current = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        var currentIds = current.Select(x => x.ChoirId).ToHashSet();

        // An inactive choir may stay on an event it already serves, but cannot be newly added.
        if (choirs.Any(x => !x.IsActive && !currentIds.Contains(x.Id)))
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.ChoirInactive);
        }

        var removed = current.Where(x => !choirIds.Contains(x.ChoirId)).ToList();
        var reason = TrimToNull(request.Reason);
        if (removed.Count > 0 && reason is null && liturgicalEvent!.Status == EventStatus.Published)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.ChoirChangeReasonRequired);
        }

        foreach (var row in removed)
        {
            eventChoirRepository.Remove(row);
        }

        // The only writer of IsLead, so the event keeps a single lead.
        foreach (var row in current.Where(x => choirIds.Contains(x.ChoirId) && x.IsLead != (x.ChoirId == request.LeadChoirId)))
        {
            var tracked = await eventChoirRepository.GetByIdAsync(row.Id, cancellationToken);
            tracked!.IsLead = row.ChoirId == request.LeadChoirId;
            tracked.Reason = reason ?? tracked.Reason;
        }

        var addedIds = choirIds.Where(x => !currentIds.Contains(x)).ToList();
        foreach (var choirId in addedIds)
        {
            await eventChoirRepository.AddAsync(
                new LiturgicalEventChoir
                {
                    Id = Guid.NewGuid(), EventId = eventId, ChoirId = choirId, IsLead = choirId == request.LeadChoirId,
                    Reason = reason,
                },
                cancellationToken);
        }

        var droppedMemberIds = await GetDroppedMemberIdsAsync(removed, choirIds, cancellationToken);
        await DropMembersFromEventAsync(eventId, droppedMemberIds, cancellationToken);

        var leadChanged = current.FirstOrDefault(x => x.IsLead)?.ChoirId != request.LeadChoirId;
        if (leadChanged)
        {
            // The song list fits the day, not the choir, so the new lead carries it on.
            foreach (var songList in await songListRepository.ListAsync(x => x.EventId == eventId, cancellationToken))
            {
                var tracked = await songListRepository.GetByIdAsync(songList.Id, cancellationToken);
                tracked!.ChoirId = request.LeadChoirId;
            }
        }

        await eventChoirRepository.SaveChangesAsync(cancellationToken);

        await NotifyChoirChangeAsync(
            liturgicalEvent!, removed.Select(x => x.ChoirId).ToList(), addedIds,
            leadChanged ? request.LeadChoirId : null, droppedMemberIds, reason, cancellationToken);

        return Result<List<LiturgicalEventChoirDto>>.Success(await LoadEventChoirsAsync(eventId, cancellationToken));
    }

    public async Task<Result<List<LiturgicalEventChoirDto>>> UpdateSubstituteDirectorAsync(
        Guid eventId, Guid choirId, UpdateSubstituteDirectorRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (EditableEventError(liturgicalEvent) is { } eventError)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(eventError);
        }

        var row = (await eventChoirRepository.ListAsync(x => x.EventId == eventId && x.ChoirId == choirId, cancellationToken))
            .SingleOrDefault();
        var choir = await choirRepository.GetByIdAsync(choirId, cancellationToken);
        if (row is null || choir is null)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.ChoirNotServingEvent);
        }

        // The choir's own director already holds these rights, so they cannot stand in for themselves.
        if (request.DirectorId is { } directorId
            && (directorId == choir.DirectorId || !await IsValidDirectorAsync(directorId, cancellationToken)))
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.ChoirDirectorInvalid);
        }

        var previousId = row.SubstituteDirectorId;
        var tracked = await eventChoirRepository.GetByIdAsync(row.Id, cancellationToken);
        tracked!.SubstituteDirectorId = request.DirectorId;
        tracked.Reason = request.DirectorId is null ? null : TrimToNull(request.Reason);
        await eventChoirRepository.SaveChangesAsync(cancellationToken);

        if (previousId != request.DirectorId)
        {
            await NotifySubstituteChangeAsync(
                liturgicalEvent!, choir, previousId, request.DirectorId, tracked.Reason, cancellationToken);
        }

        return Result<List<LiturgicalEventChoirDto>>.Success(await LoadEventChoirsAsync(eventId, cancellationToken));
    }

    /// <summary>Error code when the Parish Priest may not change the event's choirs: missing, cancelled or past.</summary>
    private static string? EditableEventError(LiturgicalEvent? liturgicalEvent) =>
        liturgicalEvent is null ? ErrorCodes.LiturgicalEventNotFound
        : liturgicalEvent.Status == EventStatus.Cancelled ? ErrorCodes.LiturgicalEventCancelled
        : liturgicalEvent.EventDate < VietnamTime.Today ? ErrorCodes.LiturgicalEventAlreadyPassed
        : null;

    /// <summary>Active members of the removed choirs who are in none of the choirs still serving the event.</summary>
    private async Task<List<Guid>> GetDroppedMemberIdsAsync(
        IReadOnlyCollection<LiturgicalEventChoir> removed, IReadOnlyCollection<Guid> keptChoirIds,
        CancellationToken cancellationToken)
    {
        if (removed.Count == 0)
        {
            return [];
        }

        var removedIds = removed.Select(x => x.ChoirId).ToList();
        var rows = await membershipRepository.ListAsync(
            x => x.IsActive && (removedIds.Contains(x.ChoirId) || keptChoirIds.Contains(x.ChoirId)), cancellationToken);
        var kept = rows.Where(x => keptChoirIds.Contains(x.ChoirId)).Select(x => x.MemberId).ToHashSet();

        return rows.Select(x => x.MemberId).Where(x => !kept.Contains(x)).Distinct().ToList();
    }

    /// <summary>
    /// Deletes the members' invitations and roster lines for the event. A finalized roster reopens so the new lead can
    /// fill the gaps; the early lock on whole-service lines lifts too when one of them went.
    /// </summary>
    private async Task DropMembersFromEventAsync(
        Guid eventId, IReadOnlyCollection<Guid> memberIds, CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
        {
            return;
        }

        foreach (var participation in await participationRepository.ListAsync(
                     x => x.EventId == eventId && memberIds.Contains(x.MemberId), cancellationToken))
        {
            participationRepository.Remove(participation);
        }

        var roster = (await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken))?.ServiceRoster;
        var lines = roster?.Assignments.Where(x => memberIds.Contains(x.MemberId)).ToList() ?? [];
        if (roster is null || lines.Count == 0)
        {
            return;
        }

        // Lines they replaced keep their Replaced status; the FK clears their ReplacedByAssignmentId.
        foreach (var line in lines)
        {
            roster.Assignments.Remove(line);
        }

        if (roster.Status == RosterStatus.Finalized)
        {
            roster.Status = RosterStatus.Draft;
            roster.FinalizedAt = null;
            roster.FinalizedBy = null;
        }

        if (lines.Any(x => x.SongListItemId is null))
        {
            roster.WholeServiceFinalizedAt = null;
        }
    }

    private async Task NotifyChoirChangeAsync(
        LiturgicalEvent liturgicalEvent, IReadOnlyCollection<Guid> removedIds, IReadOnlyCollection<Guid> addedIds,
        Guid? newLeadId, IReadOnlyCollection<Guid> droppedMemberIds, string? reason, CancellationToken cancellationToken)
    {
        var assignedIds = addedIds.Concat(newLeadId is { } leadId ? [leadId] : []).Distinct().ToList();
        var choirIds = removedIds.Concat(assignedIds).ToList();
        var choirs = (await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken))
            .ToDictionary(x => x.Id);
        var label = EventLabel(liturgicalEvent);

        foreach (var choir in removedIds.Select(choirs.GetValueOrDefault).OfType<Choir>())
        {
            await NotifyAsync(
                liturgicalEvent, [choir.DirectorId], "Ca đoàn không còn phục vụ sự kiện",
                WithReason($"Ca đoàn {choir.Name} không còn phục vụ sự kiện {label}.", reason), cancellationToken);
        }

        foreach (var choir in assignedIds.Select(choirs.GetValueOrDefault).OfType<Choir>())
        {
            var role = choir.Id == newLeadId ? "phụ trách" : "phục vụ";
            await NotifyAsync(
                liturgicalEvent, [choir.DirectorId], $"Ca đoàn được giao {role} sự kiện",
                WithReason($"Ca đoàn {choir.Name} được giao {role} sự kiện {label}.", reason), cancellationToken);
        }

        if (droppedMemberIds.Count > 0)
        {
            var userIds = (await memberRepository.ListAsync(x => droppedMemberIds.Contains(x.Id), cancellationToken))
                .Select(x => (Guid?)x.UserId)
                .ToList();
            await NotifyAsync(
                liturgicalEvent, userIds, "Huỷ tham gia sự kiện",
                WithReason(
                    $"Ca đoàn của bạn không còn phục vụ sự kiện {label}; lời mời và phân công của bạn đã được huỷ.", reason),
                cancellationToken);
        }
    }

    private async Task NotifySubstituteChangeAsync(
        LiturgicalEvent liturgicalEvent, Choir choir, Guid? previousId, Guid? substituteId, string? reason,
        CancellationToken cancellationToken)
    {
        var label = EventLabel(liturgicalEvent);
        if (substituteId is not null)
        {
            await NotifyAsync(
                liturgicalEvent, [substituteId], "Ca trưởng thay thế",
                WithReason($"Bạn được giao điều hành ca đoàn {choir.Name} thay ca trưởng ở sự kiện {label}.", reason),
                cancellationToken);
        }

        if (previousId is not null)
        {
            await NotifyAsync(
                liturgicalEvent, [previousId], "Huỷ ca trưởng thay thế",
                $"Bạn không còn điều hành ca đoàn {choir.Name} ở sự kiện {label}.", cancellationToken);
        }

        await NotifyAsync(
            liturgicalEvent, [choir.DirectorId], "Ca trưởng thay thế",
            substituteId is null
                ? $"Ca đoàn {choir.Name} không còn ca trưởng thay thế ở sự kiện {label}."
                : WithReason($"Ca đoàn {choir.Name} có ca trưởng thay thế ở sự kiện {label}.", reason),
            cancellationToken);
    }

    private async Task NotifyAsync(
        LiturgicalEvent liturgicalEvent, IEnumerable<Guid?> userIds, string title, string content,
        CancellationToken cancellationToken)
    {
        var recipients = userIds.OfType<Guid>().Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        await notificationService.SendAsync(
            new SendNotificationRequest(
                NotificationType.EventChoirChanged, title, content, recipients, nameof(LiturgicalEvent), liturgicalEvent.Id),
            cancellationToken);
    }

    private static string EventLabel(LiturgicalEvent liturgicalEvent) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}");

    private static string WithReason(string content, string? reason) =>
        reason is null ? content : $"{content} Lý do: {reason}";

    private async Task<List<LiturgicalEventChoirDto>> LoadEventChoirsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var rows = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        var choirIds = rows.Select(x => x.ChoirId).ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);

        var rowsByChoir = rows.ToDictionary(x => x.ChoirId);
        var substituteIds = rows.Where(x => x.SubstituteDirectorId is not null)
            .Select(x => x.SubstituteDirectorId!.Value)
            .ToList();
        var substituteNames = substituteIds.Count == 0
            ? []
            : (await userRepository.ListAsync(x => substituteIds.Contains(x.Id), cancellationToken))
                .ToDictionary(x => x.Id, x => x.FullName);

        // The lead first, then by name.
        return (await ToDtosAsync(choirs, cancellationToken))
            .Select(x =>
            {
                var row = rowsByChoir[x.Id];
                return new LiturgicalEventChoirDto
                {
                    ChoirId = x.Id, Name = x.Name, IsActive = x.IsActive, IsLead = row.IsLead,
                    DirectorId = x.DirectorId, DirectorFullName = x.DirectorFullName,
                    SubstituteDirectorId = row.SubstituteDirectorId,
                    SubstituteDirectorFullName =
                        row.SubstituteDirectorId is { } id ? substituteNames.GetValueOrDefault(id) : null,
                    Reason = row.Reason,
                };
            })
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// The choir if the caller may manage it: the Admin manages every choir, a Choir Director only the choirs they direct.
    /// Another director's choir comes back null, so it reads as not found.
    /// </summary>
    private async Task<Choir?> GetManagedChoirAsync(Guid choirId, CancellationToken cancellationToken)
    {
        var choir = await choirRepository.GetByIdAsync(choirId, cancellationToken);

        return choir is not null && (currentUser.RoleName == RoleNames.Admin || choir.DirectorId == currentUser.UserId)
            ? choir
            : null;
    }

    /// <summary>No director is valid; otherwise it must be an active user with the Choir Director role.</summary>
    private async Task<bool> IsValidDirectorAsync(Guid? directorId, CancellationToken cancellationToken) =>
        directorId is null
        || (await userRepository.GetActiveByRoleAsync(RoleNames.ChoirDirector, cancellationToken)).Any(x => x.Id == directorId);

    /// <summary>Maps choirs ordered by name, filling each director's name.</summary>
    private async Task<List<ChoirDto>> ToDtosAsync(IReadOnlyCollection<Choir> choirs, CancellationToken cancellationToken)
    {
        var directorIds = choirs.Where(x => x.DirectorId is not null).Select(x => x.DirectorId!.Value).Distinct().ToList();
        var names = directorIds.Count == 0
            ? []
            : (await userRepository.ListAsync(x => directorIds.Contains(x.Id), cancellationToken))
                .ToDictionary(x => x.Id, x => x.FullName);

        return choirs
            .OrderBy(x => x.Name)
            .Select(x =>
            {
                var dto = mapper.Map<ChoirDto>(x);
                dto.DirectorFullName = x.DirectorId is { } id ? names.GetValueOrDefault(id) : null;
                return dto;
            })
            .ToList();
    }

    private async Task<bool> IsNameTakenAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
        (await choirRepository.ListAsync(x => x.Name == name && x.Id != exceptId, cancellationToken)).Count > 0;

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

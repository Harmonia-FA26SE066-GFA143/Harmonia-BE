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
        if (liturgicalEvent is null)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        if (liturgicalEvent.EventDate < VietnamTime.Today)
        {
            return Result<List<LiturgicalEventChoirDto>>.Failure(ErrorCodes.LiturgicalEventAlreadyPassed);
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

        foreach (var row in current.Where(x => !choirIds.Contains(x.ChoirId)))
        {
            eventChoirRepository.Remove(row);
        }

        // The only writer of IsLead, so the event keeps a single lead.
        foreach (var row in current.Where(x => choirIds.Contains(x.ChoirId) && x.IsLead != (x.ChoirId == request.LeadChoirId)))
        {
            var tracked = await eventChoirRepository.GetByIdAsync(row.Id, cancellationToken);
            tracked!.IsLead = row.ChoirId == request.LeadChoirId;
        }

        foreach (var choirId in choirIds.Where(x => !currentIds.Contains(x)))
        {
            await eventChoirRepository.AddAsync(
                new LiturgicalEventChoir
                {
                    Id = Guid.NewGuid(), EventId = eventId, ChoirId = choirId, IsLead = choirId == request.LeadChoirId,
                },
                cancellationToken);
        }

        await eventChoirRepository.SaveChangesAsync(cancellationToken);

        return Result<List<LiturgicalEventChoirDto>>.Success(
            await ToEventChoirDtosAsync(choirs, request.LeadChoirId, cancellationToken));
    }

    private async Task<List<LiturgicalEventChoirDto>> LoadEventChoirsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var rows = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        var choirIds = rows.Select(x => x.ChoirId).ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);

        return await ToEventChoirDtosAsync(choirs, rows.FirstOrDefault(x => x.IsLead)?.ChoirId, cancellationToken);
    }

    /// <summary>The event's choirs, the lead first, then by name.</summary>
    private async Task<List<LiturgicalEventChoirDto>> ToEventChoirDtosAsync(
        IReadOnlyCollection<Choir> choirs, Guid? leadChoirId, CancellationToken cancellationToken) =>
        (await ToDtosAsync(choirs, cancellationToken))
            .Select(x => new LiturgicalEventChoirDto
            {
                ChoirId = x.Id, Name = x.Name, IsActive = x.IsActive, IsLead = x.Id == leadChoirId,
                DirectorId = x.DirectorId, DirectorFullName = x.DirectorFullName,
            })
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.Name)
            .ToList();

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

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
    IMapper mapper) : IChoirService
{
    public async Task<Result<ChoirDto>> CreateAsync(CreateChoirRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await IsNameTakenAsync(name, null, cancellationToken))
        {
            return Result<ChoirDto>.Failure(ErrorCodes.ChoirNameAlreadyExists);
        }

        var choir = new Choir { Id = Guid.NewGuid(), Name = name, Description = TrimToNull(request.Description) };
        await choirRepository.AddAsync(choir, cancellationToken);
        await choirRepository.SaveChangesAsync(cancellationToken);

        return Result<ChoirDto>.Success(mapper.Map<ChoirDto>(choir));
    }

    public async Task<Result<List<ChoirDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var choirs = await choirRepository.ListAsync(_ => true, cancellationToken);

        return Result<List<ChoirDto>>.Success(mapper.Map<List<ChoirDto>>(choirs.OrderBy(x => x.Name).ToList()));
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

        choir.Name = name;
        choir.Description = TrimToNull(request.Description);
        choir.IsActive = request.IsActive;
        await choirRepository.SaveChangesAsync(cancellationToken);

        return Result<ChoirDto>.Success(mapper.Map<ChoirDto>(choir));
    }

    public async Task<Result<List<ChoirMemberDto>>> GetMembersAsync(Guid choirId, CancellationToken cancellationToken)
    {
        if (await choirRepository.GetByIdAsync(choirId, cancellationToken) is null)
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
        var choir = await choirRepository.GetByIdAsync(choirId, cancellationToken);
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

    public async Task<Result<List<ChoirDto>>> GetEventChoirsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken) is null)
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        return Result<List<ChoirDto>>.Success(await LoadEventChoirsAsync(eventId, cancellationToken));
    }

    public async Task<Result<List<ChoirDto>>> UpdateEventChoirsAsync(
        Guid eventId, UpdateEventChoirsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken);
        if (liturgicalEvent is null)
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        }

        if (liturgicalEvent.Status == EventStatus.Cancelled)
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.LiturgicalEventCancelled);
        }

        if (liturgicalEvent.EventDate < VietnamTime.Today)
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.LiturgicalEventAlreadyPassed);
        }

        var choirIds = request.ChoirIds.Distinct().ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);
        if (choirs.Count != choirIds.Count)
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.ChoirNotFound);
        }

        var current = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        var currentIds = current.Select(x => x.ChoirId).ToHashSet();

        // An inactive choir may stay on an event it already serves, but cannot be newly added.
        if (choirs.Any(x => !x.IsActive && !currentIds.Contains(x.Id)))
        {
            return Result<List<ChoirDto>>.Failure(ErrorCodes.ChoirInactive);
        }

        foreach (var row in current.Where(x => !choirIds.Contains(x.ChoirId)))
        {
            eventChoirRepository.Remove(row);
        }

        foreach (var choirId in choirIds.Where(x => !currentIds.Contains(x)))
        {
            await eventChoirRepository.AddAsync(
                new LiturgicalEventChoir { Id = Guid.NewGuid(), EventId = eventId, ChoirId = choirId }, cancellationToken);
        }

        await eventChoirRepository.SaveChangesAsync(cancellationToken);

        return Result<List<ChoirDto>>.Success(mapper.Map<List<ChoirDto>>(choirs.OrderBy(x => x.Name).ToList()));
    }

    private async Task<List<ChoirDto>> LoadEventChoirsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var choirIds = (await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken))
            .Select(x => x.ChoirId)
            .ToList();
        var choirs = await choirRepository.ListAsync(x => choirIds.Contains(x.Id), cancellationToken);

        return mapper.Map<List<ChoirDto>>(choirs.OrderBy(x => x.Name).ToList());
    }

    private async Task<bool> IsNameTakenAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
        (await choirRepository.ListAsync(x => x.Name == name && x.Id != exceptId, cancellationToken)).Count > 0;

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

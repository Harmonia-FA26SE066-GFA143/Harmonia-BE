using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Services;

public class ChoirScopeService(
    IGenericRepository<Choir> choirRepository,
    IGenericRepository<ChoirMembership> membershipRepository,
    IGenericRepository<LiturgicalEventChoir> eventChoirRepository,
    ICurrentUserService currentUser) : IChoirScopeService
{
    private bool IsDirector => currentUser.RoleName == RoleNames.ChoirDirector;

    public async Task<IReadOnlyCollection<Guid>?> GetDirectedChoirIdsAsync(CancellationToken cancellationToken)
    {
        if (!IsDirector)
        {
            return null;
        }

        var userId = currentUser.UserId;
        return (await choirRepository.ListAsync(x => x.DirectorId == userId, cancellationToken))
            .Select(x => x.Id)
            .ToList();
    }

    public async Task<bool> CanManageChoirAsync(Guid? choirId, CancellationToken cancellationToken) =>
        choirId is null
        || await GetDirectedChoirIdsAsync(cancellationToken) is not { } mine
        || mine.Contains(choirId.Value);

    public async Task<string?> CheckChoirForNewRecordAsync(
        Guid choirId, Guid? eventId, CancellationToken cancellationToken)
    {
        var choir = await choirRepository.GetByIdAsync(choirId, cancellationToken);
        if (choir is null || !await CanManageChoirAsync(choirId, cancellationToken))
        {
            return ErrorCodes.ChoirNotFound;
        }

        if (!choir.IsActive)
        {
            return ErrorCodes.ChoirInactive;
        }

        if (eventId is null)
        {
            return null;
        }

        var eventChoirs = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        return eventChoirs.Count == 0 || eventChoirs.Any(x => x.ChoirId == choirId)
            ? null
            : ErrorCodes.ChoirNotServingEvent;
    }

    public async Task<bool> CanViewEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (await GetDirectedChoirIdsAsync(cancellationToken) is not { } mine)
        {
            return true;
        }

        var eventChoirs = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        return eventChoirs.Count == 0 || eventChoirs.Any(x => mine.Contains(x.ChoirId));
    }

    public async Task<bool> CanLeadEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        if (await GetDirectedChoirIdsAsync(cancellationToken) is not { } mine)
        {
            return true;
        }

        var eventChoirs = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);
        if (eventChoirs.Count == 0)
        {
            return true;
        }

        var leads = eventChoirs.Where(x => x.IsLead).ToList();
        return (leads.Count > 0 ? leads : eventChoirs).Any(x => mine.Contains(x.ChoirId));
    }

    public async Task<string?> CheckCanLeadEventAsync(
        Guid eventId, string notFoundCode, CancellationToken cancellationToken)
    {
        if (await CanLeadEventAsync(eventId, cancellationToken))
        {
            return null;
        }

        return await CanViewEventAsync(eventId, cancellationToken) ? ErrorCodes.ChoirNotLead : notFoundCode;
    }

    public async Task<Guid?> GetLeadChoirIdAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var eventChoirs = await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken);

        return eventChoirs.FirstOrDefault(x => x.IsLead)?.ChoirId
            ?? (eventChoirs.Count == 1 ? eventChoirs[0].ChoirId : null);
    }

    public async Task<IReadOnlyCollection<Guid>?> GetEventMemberIdsAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        var choirIds = (await eventChoirRepository.ListAsync(x => x.EventId == eventId, cancellationToken))
            .Select(x => x.ChoirId)
            .ToList();
        if (choirIds.Count == 0)
        {
            return null;
        }

        return (await membershipRepository.ListAsync(x => choirIds.Contains(x.ChoirId) && x.IsActive, cancellationToken))
            .Select(x => x.MemberId)
            .Distinct()
            .ToList();
    }

    public async Task<IReadOnlyCollection<Guid>?> GetChoirMemberIdsAsync(
        Guid? choirId, CancellationToken cancellationToken) =>
        choirId is null
            ? null
            : (await membershipRepository.ListAsync(x => x.ChoirId == choirId && x.IsActive, cancellationToken))
                .Select(x => x.MemberId)
                .ToList();

    public async Task<IReadOnlyCollection<Guid>> GetMemberChoirIdsAsync(
        Guid memberId, CancellationToken cancellationToken) =>
        (await membershipRepository.ListAsync(x => x.MemberId == memberId && x.IsActive, cancellationToken))
            .Select(x => x.ChoirId)
            .ToList();
}

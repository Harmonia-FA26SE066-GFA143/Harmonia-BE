using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class EventPersonnelRequirementService(
    IServiceRosterRepository rosterRepository,
    IGenericRepository<Skill> skillRepository,
    IChoirScopeService choirScope,
    IMapper mapper) : IEventPersonnelRequirementService
{
    public async Task<Result<List<EventPersonnelRequirementDto>>> GetAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken);
        if (liturgicalEvent is null || !await choirScope.CanViewEventAsync(eventId, cancellationToken))
            return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);

        return Result<List<EventPersonnelRequirementDto>>.Success(ToDtos(liturgicalEvent));
    }

    public async Task<Result<List<EventPersonnelRequirementDto>>> UpdateAsync(
        Guid eventId, UpdateEventPersonnelRequirementsRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken);
        if (liturgicalEvent is null) return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);
        if (await choirScope.CheckCanLeadEventAsync(eventId, ErrorCodes.LiturgicalEventNotFound, cancellationToken) is { } scopeError)
            return Result<List<EventPersonnelRequirementDto>>.Failure(scopeError);

        if (liturgicalEvent.Status == EventStatus.Cancelled)
            return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.LiturgicalEventCancelled);
        if (liturgicalEvent.EventDate < VietnamTime.Today)
            return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.LiturgicalEventAlreadyPassed);
        if (liturgicalEvent.ServiceRoster is { Status: RosterStatus.Finalized } or { WholeServiceFinalizedAt: not null })
            return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.RosterAlreadyFinalized);

        var skillIds = request.Requirements.Select(x => x.SkillId).ToList();
        var skills = skillIds.Count == 0
            ? new Dictionary<Guid, Skill>()
            : (await skillRepository.ListAsync(x => skillIds.Contains(x.Id), cancellationToken)).ToDictionary(x => x.Id);

        // Validate everything before touching the tracked event, so a failure leaves it untouched.
        var current = liturgicalEvent.PersonnelRequirements.Select(x => x.SkillId).ToHashSet();
        foreach (var skillId in skillIds)
        {
            if (!skills.TryGetValue(skillId, out var skill)) return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.SkillNotFound);
            if (!skill.IsActive && !current.Contains(skillId)) return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.SkillInactive);
        }

        var removedRequirements = liturgicalEvent.PersonnelRequirements.Where(x => !skillIds.Contains(x.SkillId)).ToList();
        var assignedSkillIds = liturgicalEvent.ServiceRoster?.Assignments
            .Where(a => a.Status == RosterAssignmentStatus.Active && a.SongListItemId == null)
            .Select(a => a.SkillId)
            .ToHashSet() ?? [];
        if (removedRequirements.Any(r => assignedSkillIds.Contains(r.SkillId)))
            return Result<List<EventPersonnelRequirementDto>>.Failure(ErrorCodes.PersonnelRequirementInUse);

        foreach (var removed in removedRequirements)
            liturgicalEvent.PersonnelRequirements.Remove(removed);

        foreach (var requirement in request.Requirements)
        {
            var existing = liturgicalEvent.PersonnelRequirements.FirstOrDefault(x => x.SkillId == requirement.SkillId);
            if (existing is not null)
            {
                existing.RequiredCount = requirement.RequiredCount;
                continue;
            }

            liturgicalEvent.PersonnelRequirements.Add(new EventPersonnelRequirement
            {
                EventId = eventId,
                SkillId = requirement.SkillId,
                Skill = skills[requirement.SkillId],
                RequiredCount = requirement.RequiredCount,
            });
        }

        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result<List<EventPersonnelRequirementDto>>.Success(ToDtos(liturgicalEvent));
    }

    private List<EventPersonnelRequirementDto> ToDtos(LiturgicalEvent liturgicalEvent) =>
        mapper.Map<List<EventPersonnelRequirementDto>>(liturgicalEvent.PersonnelRequirements.OrderBy(x => x.Skill.Name));
}

using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class RosterService(
    IServiceRosterRepository rosterRepository,
    IRosterSuggestionGenerator suggestionGenerator,
    ICurrentUserService currentUser,
    IMapper mapper) : IRosterService
{
    private const int RecentServiceDays = 60;

    private sealed record Candidate(Guid MemberId, SkillLevel? Level, int RecentServiceCount);

    /// <summary>One song / skill requirement still to be staffed; Candidates are ordered best first for the rule-based fill.</summary>
    private sealed record Slot(
        string Code, SongListItem Item, SongPersonnelRequirement Requirement, int ManualCount, List<Candidate> Candidates)
    {
        public int NeededCount => Math.Max(0, Requirement.RequiredCount - ManualCount);

        public List<Guid> Picked { get; } = [];
    }

    public async Task<Result<RosterSuggestionResponse>> SuggestAsync(SuggestServiceRosterRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.EventNotFound);
        if (liturgicalEvent.Status == EventStatus.Cancelled) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.EventCancelled);

        // ponytail: compares UTC date with the event's local date, so an event stays suggestible up to 7 hours past midnight in Vietnam.
        if (liturgicalEvent.EventDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<RosterSuggestionResponse>.Failure(ErrorCodes.EventAlreadyPassed);

        var songList = await rosterRepository.GetApprovedSongListAsync(request.EventId, cancellationToken);
        if (songList is null) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.RosterSongListNotApproved);

        var requirements = songList.Items.SelectMany(item => item.PersonnelRequirements.Select(r => (Item: item, Requirement: r))).ToList();
        if (requirements.Count == 0) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.RosterNoPersonnelRequirement);

        var roster = liturgicalEvent.ServiceRoster;
        if (roster is { Status: RosterStatus.Finalized }) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.RosterAlreadyFinalized);

        var slots = await BuildSlotsAsync(liturgicalEvent, requirements, roster, cancellationToken);
        var isAiGenerated = await PickWithAiAsync(slots, cancellationToken);
        FillByRules(slots);

        if (roster is null)
        {
            roster = new ServiceRoster { Id = Guid.NewGuid(), EventId = liturgicalEvent.Id };
            await rosterRepository.AddAsync(roster, cancellationToken);
        }

        roster.Status = RosterStatus.Suggested;
        roster.GeneratedAt = DateTime.UtcNow;
        roster.GeneratedBy = currentUser.UserId;

        foreach (var old in roster.Assignments.Where(x => x.Source == AssignmentSource.Suggested).ToList())
            roster.Assignments.Remove(old);

        foreach (var slot in slots)
        foreach (var memberId in slot.Picked)
        {
            roster.Assignments.Add(new RosterAssignment
            {
                RosterId = roster.Id,
                MemberId = memberId,
                SkillId = slot.Requirement.SkillId,
                SongListItemId = slot.Item.Id,
                Source = AssignmentSource.Suggested,
            });
        }

        await rosterRepository.SaveChangesAsync(cancellationToken);

        var assignments = await rosterRepository.GetAssignmentsAsync(roster.Id, cancellationToken);
        return Result<RosterSuggestionResponse>.Success(new RosterSuggestionResponse
        {
            RosterId = roster.Id,
            EventId = liturgicalEvent.Id,
            Status = roster.Status,
            GeneratedAt = roster.GeneratedAt,
            IsAiGenerated = isAiGenerated,
            Assignments = mapper.Map<List<RosterAssignmentDto>>(assignments),
            Shortages = slots
                .Where(x => x.Picked.Count < x.NeededCount)
                .Select(x => new RosterShortageDto
                {
                    SongListItemId = x.Item.Id,
                    SongTitle = x.Item.Song.Title,
                    SkillId = x.Requirement.SkillId,
                    SkillName = x.Requirement.Skill.Name,
                    RequiredCount = x.Requirement.RequiredCount,
                    AssignedCount = x.ManualCount + x.Picked.Count,
                })
                .ToList(),
        });
    }

    /// <summary>
    /// One slot per requirement. Manual assignments already on the roster count as filled and their
    /// members are not candidates again for that slot. A member may fill several skills of one song.
    /// </summary>
    private async Task<List<Slot>> BuildSlotsAsync(
        LiturgicalEvent liturgicalEvent, List<(SongListItem Item, SongPersonnelRequirement Requirement)> requirements,
        ServiceRoster? roster, CancellationToken cancellationToken)
    {
        var skillIds = requirements.Select(x => x.Requirement.SkillId).Distinct().ToList();
        var candidateSkills = await rosterRepository.GetCandidateSkillsAsync(liturgicalEvent.Id, skillIds, cancellationToken);

        var memberIds = candidateSkills.Select(x => x.MemberId).Distinct().ToList();
        var recent = memberIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await rosterRepository.CountRecentServicesAsync(
                memberIds, liturgicalEvent.EventDate.AddDays(-RecentServiceDays), liturgicalEvent.EventDate, cancellationToken);

        var manual = roster?.Assignments.Where(x => x.Source == AssignmentSource.Manual).ToList() ?? [];

        return requirements.Select((x, index) =>
        {
            var manualMemberIds = manual
                .Where(a => a.SongListItemId == x.Item.Id && a.SkillId == x.Requirement.SkillId)
                .Select(a => a.MemberId)
                .ToHashSet();

            var candidates = candidateSkills
                .Where(c => c.SkillId == x.Requirement.SkillId && !manualMemberIds.Contains(c.MemberId))
                .Select(c => new Candidate(c.MemberId, c.Level, recent.GetValueOrDefault(c.MemberId)))
                .OrderByDescending(c => c.Level.HasValue ? (int)c.Level.Value : -1)
                .ThenBy(c => c.RecentServiceCount)
                .ThenBy(c => c.MemberId)
                .ToList();

            return new Slot($"S{index + 1}", x.Item, x.Requirement, manualMemberIds.Count, candidates);
        }).ToList();
    }

    /// <summary>
    /// Asks the AI to pick within each slot's candidates, then keeps only picks that are real candidates of
    /// that slot, not repeated, and within the needed count. Returns false when the AI was not used or failed.
    /// </summary>
    private async Task<bool> PickWithAiAsync(List<Slot> slots, CancellationToken cancellationToken)
    {
        var open = slots.Where(x => x.NeededCount > 0 && x.Candidates.Count > 0).ToList();
        if (open.Count == 0) return false;

        // Codes instead of ids, and no names or contact details, ever leave the server.
        var codes = open.SelectMany(x => x.Candidates).Select(x => x.MemberId).Distinct()
            .Select((id, index) => (Id: id, Code: $"M{index + 1}"))
            .ToList();
        var codeOf = codes.ToDictionary(x => x.Id, x => x.Code);
        var idOf = codes.ToDictionary(x => x.Code, x => x.Id);

        var input = open.Select(x => new RosterSlotInput
        {
            SlotCode = x.Code,
            SkillName = x.Requirement.Skill.Name,
            NeededCount = x.NeededCount,
            Candidates = x.Candidates.Select(c => new RosterCandidateInput
            {
                MemberCode = codeOf[c.MemberId],
                Level = c.Level,
                RecentServiceCount = c.RecentServiceCount,
            }).ToList(),
        }).ToList();

        var result = await suggestionGenerator.GenerateAsync(input, cancellationToken);
        if (!result.IsSuccess) return false;

        var slotByCode = open.ToDictionary(x => x.Code);
        foreach (var pick in result.Value!)
        {
            if (!slotByCode.TryGetValue(pick.SlotCode, out var slot)) continue;

            foreach (var code in pick.MemberCodes)
            {
                if (slot.Picked.Count >= slot.NeededCount) break;
                if (idOf.TryGetValue(code, out var memberId)
                    && slot.Candidates.Any(c => c.MemberId == memberId)
                    && !slot.Picked.Contains(memberId))
                    slot.Picked.Add(memberId);
            }
        }

        return true;
    }

    /// <summary>Tops every slot up from its remaining candidates, best first. The whole fallback when the AI failed.</summary>
    private static void FillByRules(List<Slot> slots)
    {
        foreach (var slot in slots)
        foreach (var candidate in slot.Candidates)
        {
            if (slot.Picked.Count >= slot.NeededCount) break;
            if (!slot.Picked.Contains(candidate.MemberId)) slot.Picked.Add(candidate.MemberId);
        }
    }
}

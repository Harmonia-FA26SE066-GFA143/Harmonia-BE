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
    INotificationService notificationService,
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
        if (EventNotEditableCode(liturgicalEvent) is { } eventError) return Result<RosterSuggestionResponse>.Failure(eventError);

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
        roster.GeneratedAt = DateTime.Now;
        roster.GeneratedBy = currentUser.UserId;

        // Replaced lines stay as history even when they came from a suggestion.
        foreach (var old in roster.Assignments
                     .Where(x => x.Source == AssignmentSource.Suggested && x.Status == RosterAssignmentStatus.Active).ToList())
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

    public async Task<Result<List<RosterShortageDto>>> GetShortagesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken);
        if (liturgicalEvent is null) return Result<List<RosterShortageDto>>.Failure(ErrorCodes.EventNotFound);

        var songList = await rosterRepository.GetApprovedSongListAsync(eventId, cancellationToken);
        if (songList is null) return Result<List<RosterShortageDto>>.Failure(ErrorCodes.RosterSongListNotApproved);

        var assignments = liturgicalEvent.ServiceRoster?.Assignments.Where(x => x.Status == RosterAssignmentStatus.Active).ToList() ?? [];

        return Result<List<RosterShortageDto>>.Success(ComputeShortages(songList, assignments));
    }

    public async Task<Result<ServiceRosterDto>> FinalizeAsync(Guid rosterId, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterWithEventAsync(rosterId, cancellationToken);
        if (roster is null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterNotFound);
        if (RosterNotEditableCode(roster) is { } rosterError) return Result<ServiceRosterDto>.Failure(rosterError);

        var songList = await rosterRepository.GetApprovedSongListAsync(roster.EventId, cancellationToken);
        if (songList is null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterSongListNotApproved);

        // Members may have left, lost the skill or withdrawn since they were assigned.
        var assignments = await rosterRepository.GetAssignmentsWithEligibilityAsync(roster.Id, roster.EventId, cancellationToken);
        foreach (var assignment in assignments)
        {
            var code = assignment.Member.Status != MemberStatus.Active ? ErrorCodes.MemberNotActive
                : !assignment.Member.MemberSkills.Any(s => s.SkillId == assignment.SkillId) ? ErrorCodes.AssignmentMemberSkillNotApproved
                : assignment.Member.EventParticipations.Count == 0 ? ErrorCodes.AssignmentMemberNotConfirmed
                : null;
            if (code is not null)
                return Result<ServiceRosterDto>.Failure(code, $"Assignment {assignment.Id} is no longer valid.");
        }

        roster.Status = RosterStatus.Finalized;
        roster.FinalizedAt = DateTime.Now;
        roster.FinalizedBy = currentUser.UserId;
        await rosterRepository.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<ServiceRosterDto>(roster);
        dto.Assignments = mapper.Map<List<RosterAssignmentDto>>(assignments);
        dto.Shortages = ComputeShortages(songList, assignments);
        return Result<ServiceRosterDto>.Success(dto);
    }

    public async Task<Result> SendNotificationsAsync(
        Guid rosterId, SendRosterNotificationsRequest request, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterForNotificationAsync(rosterId, cancellationToken);
        if (roster is null) return Result.Failure(ErrorCodes.RosterNotFound);
        if (EventNotEditableCode(roster.LiturgicalEvent) is { } eventError) return Result.Failure(eventError);
        if (roster.Status != RosterStatus.Finalized) return Result.Failure(ErrorCodes.RosterNotFinalized);

        var linesByMember = roster.Assignments
            .Where(x => x.Status == RosterAssignmentStatus.Active)
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SongListItem?.DisplayOrder).ToList());

        var memberIds = request.MemberIds.Distinct().ToList();
        if (memberIds.Any(id => !linesByMember.ContainsKey(id))) return Result.Failure(ErrorCodes.AssignmentNotFound);
        if (memberIds.Count == 0)
            memberIds = linesByMember.Where(x => x.Value.Any(a => a.NotifiedAt is null)).Select(x => x.Key).ToList();

        var liturgicalEvent = roster.LiturgicalEvent;
        var eventLabel = $"{liturgicalEvent.Title ?? "the event"} on {liturgicalEvent.EventDate:dd/MM/yyyy} at {liturgicalEvent.Time:HH:mm}";

        foreach (var memberId in memberIds)
        {
            var lines = linesByMember[memberId];
            var notifiedAt = DateTime.Now;
            foreach (var line in lines) line.NotifiedAt = notifiedAt;

            // SendAsync saves through the same scoped DbContext, so each member's NotifiedAt is stored with their notification.
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.AssignmentNotice,
                    "Service assignment",
                    $"You are assigned to serve at {eventLabel}: "
                        + string.Join("; ", lines.Select(x => x.SongListItem is null ? x.Skill.Name : $"{x.Skill.Name} - {x.SongListItem.Song.Title}"))
                        + ".",
                    [lines[0].Member.UserId],
                    nameof(LiturgicalEvent),
                    roster.EventId),
                cancellationToken);
        }

        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Requirements of the song list that the given Active assignments do not fully staff, in song order.</summary>
    private static List<RosterShortageDto> ComputeShortages(SongList songList, List<RosterAssignment> activeAssignments) =>
        songList.Items
            .OrderBy(item => item.DisplayOrder)
            .SelectMany(item => item.PersonnelRequirements.Select(r => new RosterShortageDto
            {
                SongListItemId = item.Id,
                SongTitle = item.Song.Title,
                SkillId = r.SkillId,
                SkillName = r.Skill.Name,
                RequiredCount = r.RequiredCount,
                AssignedCount = activeAssignments.Count(a => a.SongListItemId == item.Id && a.SkillId == r.SkillId),
            }))
            .Where(x => x.AssignedCount < x.RequiredCount)
            .ToList();

    public async Task<Result<RosterAssignmentDto>> AddAssignmentAsync(CreateRosterAssignmentRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.EventNotFound);
        if (EventNotEditableCode(liturgicalEvent) is { } eventError) return Result<RosterAssignmentDto>.Failure(eventError);

        var roster = liturgicalEvent.ServiceRoster;
        if (roster is { Status: RosterStatus.Finalized }) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterAlreadyFinalized);

        var songList = await rosterRepository.GetApprovedSongListAsync(request.EventId, cancellationToken);
        if (songList is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterSongListNotApproved);
        if (!songList.Items.Any(i => i.Id == request.SongListItemId && i.PersonnelRequirements.Any(r => r.SkillId == request.SkillId)))
            return Result<RosterAssignmentDto>.Failure(ErrorCodes.PersonnelRequirementNotFound);

        var memberError = await MemberNotAssignableCodeAsync(
            roster, liturgicalEvent.Id, request.SongListItemId, request.SkillId, request.MemberId, cancellationToken);
        if (memberError is not null) return Result<RosterAssignmentDto>.Failure(memberError);

        if (roster is null)
        {
            roster = new ServiceRoster { Id = Guid.NewGuid(), EventId = liturgicalEvent.Id, Status = RosterStatus.Draft };
            await rosterRepository.AddAsync(roster, cancellationToken);
        }

        var assignment = NewManualAssignment(roster.Id, request.SongListItemId, request.SkillId, request.MemberId);
        roster.Assignments.Add(assignment);
        await rosterRepository.SaveChangesAsync(cancellationToken);

        return Result<RosterAssignmentDto>.Success(await GetAssignmentDtoAsync(roster.Id, assignment.Id, cancellationToken));
    }

    public async Task<Result<RosterAssignmentDto>> ReplaceAssignmentAsync(
        Guid assignmentId, ReplaceRosterAssignmentRequest request, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterByAssignmentAsync(assignmentId, cancellationToken);
        var old = roster?.Assignments.FirstOrDefault(x => x.Id == assignmentId && x.Status == RosterAssignmentStatus.Active);
        if (roster is null || old is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.AssignmentNotFound);
        if (RosterNotEditableCode(roster) is { } rosterError) return Result<RosterAssignmentDto>.Failure(rosterError);

        var memberError = await MemberNotAssignableCodeAsync(
            roster, roster.EventId, old.SongListItemId, old.SkillId, request.MemberId, cancellationToken);
        if (memberError is not null) return Result<RosterAssignmentDto>.Failure(memberError);

        var replacement = NewManualAssignment(roster.Id, old.SongListItemId, old.SkillId, request.MemberId);
        roster.Assignments.Add(replacement);
        old.Status = RosterAssignmentStatus.Replaced;
        old.ReplacedByAssignmentId = replacement.Id;
        await rosterRepository.SaveChangesAsync(cancellationToken);

        return Result<RosterAssignmentDto>.Success(await GetAssignmentDtoAsync(roster.Id, replacement.Id, cancellationToken));
    }

    public async Task<Result> RemoveAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterByAssignmentAsync(assignmentId, cancellationToken);
        var assignment = roster?.Assignments.FirstOrDefault(x => x.Id == assignmentId && x.Status == RosterAssignmentStatus.Active);
        if (roster is null || assignment is null) return Result.Failure(ErrorCodes.AssignmentNotFound);
        if (RosterNotEditableCode(roster) is { } rosterError) return Result.Failure(rosterError);

        // A line it replaced keeps its Replaced status; the FK clears its ReplacedByAssignmentId.
        roster.Assignments.Remove(assignment);
        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Error code when the event's roster can no longer change, or null when it can.</summary>
    private static string? EventNotEditableCode(LiturgicalEvent liturgicalEvent)
    {
        if (liturgicalEvent.Status == EventStatus.Cancelled) return ErrorCodes.EventCancelled;

        // ponytail: compares UTC date with the event's local date, so an event stays editable up to 7 hours past midnight in Vietnam.
        return liturgicalEvent.EventDate < DateOnly.FromDateTime(DateTime.Now) ? ErrorCodes.EventAlreadyPassed : null;
    }

    private static string? RosterNotEditableCode(ServiceRoster roster) =>
        EventNotEditableCode(roster.LiturgicalEvent)
        ?? (roster.Status == RosterStatus.Finalized ? ErrorCodes.RosterAlreadyFinalized : null);

    /// <summary>
    /// Error code when the member cannot take the song / skill: already holds it on the roster, missing, not Active,
    /// skill not approved or participation not confirmed. Null when they can.
    /// </summary>
    private async Task<string?> MemberNotAssignableCodeAsync(
        ServiceRoster? roster, Guid eventId, Guid? songListItemId, Guid skillId, Guid memberId, CancellationToken cancellationToken)
    {
        if (roster?.Assignments.Any(a => a.Status == RosterAssignmentStatus.Active
                && a.SongListItemId == songListItemId && a.SkillId == skillId && a.MemberId == memberId) == true)
            return ErrorCodes.AssignmentDuplicate;

        var member = await rosterRepository.GetMemberForAssignmentAsync(memberId, skillId, eventId, cancellationToken);
        if (member is null) return ErrorCodes.MemberNotFound;
        if (member.Status != MemberStatus.Active) return ErrorCodes.MemberNotActive;
        if (member.MemberSkills.Count == 0) return ErrorCodes.AssignmentMemberSkillNotApproved;
        if (member.EventParticipations.Count == 0) return ErrorCodes.AssignmentMemberNotConfirmed;
        return null;
    }

    private static RosterAssignment NewManualAssignment(Guid rosterId, Guid? songListItemId, Guid skillId, Guid memberId) => new()
    {
        Id = Guid.NewGuid(),
        RosterId = rosterId,
        MemberId = memberId,
        SkillId = skillId,
        SongListItemId = songListItemId,
        Source = AssignmentSource.Manual,
    };

    private async Task<RosterAssignmentDto> GetAssignmentDtoAsync(Guid rosterId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var assignments = await rosterRepository.GetAssignmentsAsync(rosterId, cancellationToken);
        return mapper.Map<RosterAssignmentDto>(assignments.First(x => x.Id == assignmentId));
    }

    /// <summary>
    /// One slot per requirement. Manual assignments already on the roster count as filled and their
    /// members are not candidates again for that slot, nor are members the Choir Director replaced in it.
    /// A member may fill several skills of one song.
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

        var kept = roster?.Assignments
            .Where(x => x.Source == AssignmentSource.Manual || x.Status == RosterAssignmentStatus.Replaced)
            .ToList() ?? [];

        return requirements.Select((x, index) =>
        {
            var inSlot = kept.Where(a => a.SongListItemId == x.Item.Id && a.SkillId == x.Requirement.SkillId).ToList();
            var manualCount = inSlot.Count(a => a.Status == RosterAssignmentStatus.Active);
            var excludedMemberIds = inSlot.Select(a => a.MemberId).ToHashSet();

            var candidates = candidateSkills
                .Where(c => c.SkillId == x.Requirement.SkillId && !excludedMemberIds.Contains(c.MemberId))
                .Select(c => new Candidate(c.MemberId, c.Level, recent.GetValueOrDefault(c.MemberId)))
                .OrderByDescending(c => c.Level.HasValue ? (int)c.Level.Value : -1)
                .ThenBy(c => c.RecentServiceCount)
                .ThenBy(c => c.MemberId)
                .ToList();

            return new Slot($"S{index + 1}", x.Item, x.Requirement, manualCount, candidates);
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

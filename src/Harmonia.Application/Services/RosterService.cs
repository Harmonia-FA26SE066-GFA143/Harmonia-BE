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
    ISystemSettingService systemSettingService,
    IChoirScopeService choirScope,
    IMapper mapper) : IRosterService
{
    private sealed record Candidate(Guid MemberId, SkillLevel? Level, int RecentServiceCount);

    /// <summary>One requirement of the event: Item is null for a whole-service one (organist, conductor...).</summary>
    private sealed record Need(SongListItem? Item, Guid SkillId, Skill Skill, int RequiredCount);

    /// <summary>One requirement still to be staffed; Candidates are ordered best first for the rule-based fill.</summary>
    private sealed record Slot(string Code, Need Need, int ManualCount, List<Candidate> Candidates)
    {
        public int NeededCount => Math.Max(0, Need.RequiredCount - ManualCount);

        public List<Guid> Picked { get; } = [];
    }

    public async Task<Result<RosterSuggestionResponse>> SuggestAsync(SuggestServiceRosterRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.LiturgicalEventNotFound);
        if (await choirScope.CheckCanLeadEventAsync(liturgicalEvent.Id, ErrorCodes.LiturgicalEventNotFound, cancellationToken)
            is { } scopeError)
            return Result<RosterSuggestionResponse>.Failure(scopeError);
        if (EventNotEditableCode(liturgicalEvent) is { } eventError) return Result<RosterSuggestionResponse>.Failure(eventError);

        // Whole-service requirements can be staffed before the song list is approved; song ones cannot.
        var songList = await rosterRepository.GetApprovedSongListAsync(request.EventId, cancellationToken);
        if (songList is null && liturgicalEvent.PersonnelRequirements.Count == 0)
            return Result<RosterSuggestionResponse>.Failure(ErrorCodes.RosterSongListNotApproved);

        var roster = liturgicalEvent.ServiceRoster;
        var wholeServiceLocked = roster?.WholeServiceFinalizedAt is not null;

        // Locked whole-service lines are neither re-picked nor reported here.
        var requirements = CollectNeeds(liturgicalEvent, songList).Where(x => x.Item is not null || !wholeServiceLocked).ToList();
        if (requirements.Count == 0) return Result<RosterSuggestionResponse>.Failure(ErrorCodes.RosterNoPersonnelRequirement);

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

        // Replaced lines stay as history even when they came from a suggestion.
        foreach (var old in roster.Assignments
                     .Where(x => x.Source == AssignmentSource.Suggested && x.Status == RosterAssignmentStatus.Active
                         && (x.SongListItemId is not null || !wholeServiceLocked))
                     .ToList())
            roster.Assignments.Remove(old);

        foreach (var slot in slots)
        {
            foreach (var memberId in slot.Picked)
            {
                roster.Assignments.Add(new RosterAssignment
                {
                    RosterId = roster.Id,
                    MemberId = memberId,
                    SkillId = slot.Need.SkillId,
                    SongListItemId = slot.Need.Item?.Id,
                    Source = AssignmentSource.Suggested,
                });
            }
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
                .Select(x => ToShortage(x.Need, x.ManualCount + x.Picked.Count))
                .ToList(),
        });
    }

    public async Task<Result<List<RosterShortageDto>>> GetShortagesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken);
        if (liturgicalEvent is null || !await choirScope.CanViewEventAsync(eventId, cancellationToken))
            return Result<List<RosterShortageDto>>.Failure(ErrorCodes.LiturgicalEventNotFound);

        var songList = await rosterRepository.GetApprovedSongListAsync(eventId, cancellationToken);
        if (songList is null && liturgicalEvent.PersonnelRequirements.Count == 0)
            return Result<List<RosterShortageDto>>.Failure(ErrorCodes.RosterSongListNotApproved);

        var assignments = liturgicalEvent.ServiceRoster?.Assignments.Where(x => x.Status == RosterAssignmentStatus.Active).ToList() ?? [];

        return Result<List<RosterShortageDto>>.Success(ComputeShortages(liturgicalEvent, songList, assignments));
    }

    public async Task<Result<ServiceRosterDto>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(eventId, cancellationToken);
        if (liturgicalEvent is null || !await choirScope.CanViewEventAsync(eventId, cancellationToken))
            return Result<ServiceRosterDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        if (liturgicalEvent.ServiceRoster is not { } roster) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterNotFound);

        var assignments = await rosterRepository.GetAssignmentsAsync(roster.Id, cancellationToken);
        var songList = await rosterRepository.GetApprovedSongListAsync(eventId, cancellationToken);

        var dto = mapper.Map<ServiceRosterDto>(roster);
        dto.Assignments = mapper.Map<List<RosterAssignmentDto>>(assignments);
        dto.Shortages = ComputeShortages(liturgicalEvent, songList, assignments);
        return Result<ServiceRosterDto>.Success(dto);
    }

    public async Task<Result<ServiceRosterDto>> FinalizeAsync(Guid rosterId, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterWithEventAsync(rosterId, cancellationToken);
        if (roster is null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterNotFound, cancellationToken)
            is { } scopeError)
            return Result<ServiceRosterDto>.Failure(scopeError);
        if (RosterNotEditableCode(roster) is { } rosterError) return Result<ServiceRosterDto>.Failure(rosterError);

        var songList = await rosterRepository.GetApprovedSongListAsync(roster.EventId, cancellationToken);
        if (songList is null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterSongListNotApproved);

        var assignments = await rosterRepository.GetAssignmentsWithEligibilityAsync(roster.Id, roster.EventId, cancellationToken);
        if (FirstIneligible(assignments) is { } ineligible)
            return Result<ServiceRosterDto>.Failure(ineligible.Code, $"Assignment {ineligible.AssignmentId} is no longer valid.");

        roster.Status = RosterStatus.Finalized;
        roster.FinalizedAt = DateTime.UtcNow;
        roster.FinalizedBy = currentUser.UserId;
        await rosterRepository.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<ServiceRosterDto>(roster);
        dto.Assignments = mapper.Map<List<RosterAssignmentDto>>(assignments);
        dto.Shortages = ComputeShortages(roster.LiturgicalEvent, songList, assignments);
        return Result<ServiceRosterDto>.Success(dto);
    }

    public async Task<Result<ServiceRosterDto>> FinalizeWholeServiceAsync(Guid rosterId, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterWithEventAsync(rosterId, cancellationToken);
        if (roster is null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterNotFound, cancellationToken)
            is { } scopeError)
            return Result<ServiceRosterDto>.Failure(scopeError);
        if (RosterNotEditableCode(roster) is { } rosterError) return Result<ServiceRosterDto>.Failure(rosterError);
        if (roster.WholeServiceFinalizedAt is not null) return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterAlreadyFinalized);
        if (roster.LiturgicalEvent.PersonnelRequirements.Count == 0)
            return Result<ServiceRosterDto>.Failure(ErrorCodes.RosterNoPersonnelRequirement);

        var assignments = await rosterRepository.GetAssignmentsWithEligibilityAsync(roster.Id, roster.EventId, cancellationToken);
        if (FirstIneligible(assignments.Where(x => x.SongListItemId is null)) is { } ineligible)
            return Result<ServiceRosterDto>.Failure(ineligible.Code, $"Assignment {ineligible.AssignmentId} is no longer valid.");

        roster.WholeServiceFinalizedAt = DateTime.UtcNow;
        await rosterRepository.SaveChangesAsync(cancellationToken);

        var songList = await rosterRepository.GetApprovedSongListAsync(roster.EventId, cancellationToken);
        var dto = mapper.Map<ServiceRosterDto>(roster);
        dto.Assignments = mapper.Map<List<RosterAssignmentDto>>(assignments);
        dto.Shortages = ComputeShortages(roster.LiturgicalEvent, songList, assignments);
        return Result<ServiceRosterDto>.Success(dto);
    }

    /// <summary>
    /// First line whose member has since left, lost the skill or withdrawn from the event, with the code to report;
    /// null when every line is still valid. Lines must come from GetAssignmentsWithEligibilityAsync.
    /// </summary>
    private static (string Code, Guid AssignmentId)? FirstIneligible(IEnumerable<RosterAssignment> assignments)
    {
        foreach (var assignment in assignments)
        {
            var code = assignment switch
            {
                { Member.Status: not MemberStatus.Active } => ErrorCodes.MemberNotActive,
                _ when !assignment.Member.MemberSkills.Any(s => s.SkillId == assignment.SkillId) => ErrorCodes.RosterAssignmentMemberSkillNotApproved,
                { Member.EventParticipations.Count: 0 } => ErrorCodes.RosterAssignmentMemberNotConfirmed,
                _ => null,
            };
            if (code is not null) return (code, assignment.Id);
        }

        return null;
    }

    public async Task<Result> SendNotificationsAsync(
        Guid rosterId, SendRosterNotificationsRequest request, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterForNotificationAsync(rosterId, cancellationToken);
        if (roster is null) return Result.Failure(ErrorCodes.RosterNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterNotFound, cancellationToken)
            is { } scopeError)
            return Result.Failure(scopeError);
        if (EventNotEditableCode(roster.LiturgicalEvent) is { } eventError) return Result.Failure(eventError);
        if (roster.Status != RosterStatus.Finalized && roster.WholeServiceFinalizedAt is null)
            return Result.Failure(ErrorCodes.RosterNotFinalized);

        // Before the full finalization only the locked whole-service lines may be announced.
        var linesByMember = roster.Assignments
            .Where(x => x.Status == RosterAssignmentStatus.Active
                && (roster.Status == RosterStatus.Finalized || x.SongListItemId is null))
            .GroupBy(x => x.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SongListItem?.DisplayOrder).ToList());

        var memberIds = request.MemberIds.Distinct().ToList();
        if (memberIds.Any(id => !linesByMember.ContainsKey(id))) return Result.Failure(ErrorCodes.RosterAssignmentNotFound);
        if (memberIds.Count == 0)
            memberIds = linesByMember.Where(x => x.Value.Any(a => a.NotifiedAt is null)).Select(x => x.Key).ToList();

        var liturgicalEvent = roster.LiturgicalEvent;
        var eventLabel = $"{liturgicalEvent.Title ?? "sự kiện"} ngày {liturgicalEvent.EventDate:dd/MM/yyyy} lúc {liturgicalEvent.Time:HH:mm}";

        foreach (var memberId in memberIds)
        {
            var lines = linesByMember[memberId];
            var notifiedAt = DateTime.UtcNow;
            foreach (var line in lines) line.NotifiedAt = notifiedAt;

            // SendAsync saves through the same scoped DbContext, so each member's NotifiedAt is stored with their notification.
            await notificationService.SendAsync(
                new SendNotificationRequest(
                    NotificationType.AssignmentNotice,
                    "Phân công phục vụ",
                    $"Bạn được phân công phục vụ tại {eventLabel}: "
                        + string.Join("; ", lines.Select(DescribeLine))
                        + ".",
                    [lines[0].Member.UserId],
                    nameof(LiturgicalEvent),
                    roster.EventId),
                cancellationToken);
        }

        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>"Organ (cả buổi lễ)" or "Solo - Lễ vật đơn sơ", followed by the role note when there is one.</summary>
    private static string DescribeLine(RosterAssignment line) =>
        (line.SongListItem is null ? $"{line.Skill.Name} (cả buổi lễ)" : $"{line.Skill.Name} - {line.SongListItem.Song.Title}")
        + (line.Note is null ? string.Empty : $" ({line.Note})");

    /// <summary>
    /// Whole-service requirements first (by skill name), then song ones in song order. The whole-service people play every
    /// song, so a song needs only the people beyond them: Guitar x2 on a song with Guitar x1 for the service leaves Guitar x1
    /// on that song; a song asking no more than the service has is covered and skipped.
    /// </summary>
    private static List<Need> CollectNeeds(LiturgicalEvent liturgicalEvent, SongList? songList)
    {
        var wholeServiceCount = liturgicalEvent.PersonnelRequirements.ToDictionary(x => x.SkillId, x => x.RequiredCount);

        return liturgicalEvent.PersonnelRequirements
            .OrderBy(x => x.Skill.Name)
            .Select(x => new Need(null, x.SkillId, x.Skill, x.RequiredCount))
            .Concat((songList?.Items ?? [])
                .OrderBy(item => item.DisplayOrder)
                .SelectMany(item => item.PersonnelRequirements
                    .Select(r => new Need(item, r.SkillId, r.Skill, r.RequiredCount - wholeServiceCount.GetValueOrDefault(r.SkillId)))
                    .Where(n => n.RequiredCount > 0)))
            .ToList();
    }

    private static RosterShortageDto ToShortage(Need need, int assignedCount) => new()
    {
        SongListItemId = need.Item?.Id,
        SongTitle = need.Item?.Song.Title,
        SkillId = need.SkillId,
        SkillName = need.Skill.Name,
        RequiredCount = need.RequiredCount,
        AssignedCount = assignedCount,
    };

    /// <summary>Requirements the given Active assignments do not fully staff, ordered like <see cref="CollectNeeds"/>.</summary>
    private static List<RosterShortageDto> ComputeShortages(
        LiturgicalEvent liturgicalEvent, SongList? songList, List<RosterAssignment> activeAssignments) =>
        CollectNeeds(liturgicalEvent, songList)
            .Select(n => ToShortage(n, activeAssignments.Count(a => a.SongListItemId == n.Item?.Id && a.SkillId == n.SkillId)))
            .Where(x => x.AssignedCount < x.RequiredCount)
            .ToList();

    public async Task<Result<RosterAssignmentDto>> AddAssignmentAsync(CreateRosterAssignmentRequest request, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await rosterRepository.GetEventWithRosterAsync(request.EventId, cancellationToken);
        if (liturgicalEvent is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.LiturgicalEventNotFound);
        if (await choirScope.CheckCanLeadEventAsync(liturgicalEvent.Id, ErrorCodes.LiturgicalEventNotFound, cancellationToken)
            is { } scopeError)
            return Result<RosterAssignmentDto>.Failure(scopeError);
        if (EventNotEditableCode(liturgicalEvent) is { } eventError) return Result<RosterAssignmentDto>.Failure(eventError);

        var roster = liturgicalEvent.ServiceRoster;
        if (roster is { Status: RosterStatus.Finalized }) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterAlreadyFinalized);
        if (request.SongListItemId is null && roster?.WholeServiceFinalizedAt is not null)
            return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterAlreadyFinalized);

        if (request.SongListItemId is null)
        {
            if (liturgicalEvent.PersonnelRequirements.All(r => r.SkillId != request.SkillId))
                return Result<RosterAssignmentDto>.Failure(ErrorCodes.PersonnelRequirementNotFound);
        }
        else
        {
            var songList = await rosterRepository.GetApprovedSongListAsync(request.EventId, cancellationToken);
            if (songList is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterSongListNotApproved);
            if (!songList.Items.Any(i => i.Id == request.SongListItemId && i.PersonnelRequirements.Any(r => r.SkillId == request.SkillId)))
                return Result<RosterAssignmentDto>.Failure(ErrorCodes.PersonnelRequirementNotFound);
        }

        var memberError = await MemberNotAssignableCodeAsync(
            roster, liturgicalEvent.Id, request.SongListItemId, request.SkillId, request.MemberId, cancellationToken);
        if (memberError is not null) return Result<RosterAssignmentDto>.Failure(memberError);

        if (roster is null)
        {
            roster = new ServiceRoster { Id = Guid.NewGuid(), EventId = liturgicalEvent.Id, Status = RosterStatus.Draft };
            await rosterRepository.AddAsync(roster, cancellationToken);
        }

        var assignment = NewManualAssignment(roster.Id, request.SongListItemId, request.SkillId, request.MemberId);
        assignment.Note = NormalizeNote(request.Note);
        roster.Assignments.Add(assignment);
        await rosterRepository.SaveChangesAsync(cancellationToken);

        return Result<RosterAssignmentDto>.Success(await GetAssignmentDtoAsync(roster.Id, assignment.Id, cancellationToken));
    }

    public async Task<Result<RosterAssignmentDto>> ReplaceAssignmentAsync(
        Guid assignmentId, ReplaceRosterAssignmentRequest request, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterByAssignmentAsync(assignmentId, cancellationToken);
        var old = roster?.Assignments.FirstOrDefault(x => x.Id == assignmentId && x.Status == RosterAssignmentStatus.Active);
        if (roster is null || old is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterAssignmentNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterAssignmentNotFound, cancellationToken)
            is { } scopeError)
            return Result<RosterAssignmentDto>.Failure(scopeError);
        // A locked whole-service line can still change hands (the organist falls ill); the new member is told separately.
        if (RosterNotEditableCode(roster) is { } rosterError) return Result<RosterAssignmentDto>.Failure(rosterError);

        var memberError = await MemberNotAssignableCodeAsync(
            roster, roster.EventId, old.SongListItemId, old.SkillId, request.MemberId, cancellationToken);
        if (memberError is not null) return Result<RosterAssignmentDto>.Failure(memberError);

        var replacement = NewManualAssignment(roster.Id, old.SongListItemId, old.SkillId, request.MemberId);
        replacement.Note = request.Note is null ? old.Note : NormalizeNote(request.Note);
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
        if (roster is null || assignment is null) return Result.Failure(ErrorCodes.RosterAssignmentNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterAssignmentNotFound, cancellationToken)
            is { } scopeError)
            return Result.Failure(scopeError);
        if (LineNotEditableCode(roster, assignment) is { } rosterError) return Result.Failure(rosterError);

        // A line it replaced keeps its Replaced status; the FK clears its ReplacedByAssignmentId.
        roster.Assignments.Remove(assignment);
        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<RosterAssignmentDto>> UpdateNoteAsync(
        Guid assignmentId, UpdateRosterAssignmentNoteRequest request, CancellationToken cancellationToken)
    {
        var roster = await rosterRepository.GetRosterByAssignmentAsync(assignmentId, cancellationToken);
        var assignment = roster?.Assignments.FirstOrDefault(x => x.Id == assignmentId && x.Status == RosterAssignmentStatus.Active);
        if (roster is null || assignment is null) return Result<RosterAssignmentDto>.Failure(ErrorCodes.RosterAssignmentNotFound);
        if (await choirScope.CheckCanLeadEventAsync(roster.EventId, ErrorCodes.RosterAssignmentNotFound, cancellationToken)
            is { } scopeError)
            return Result<RosterAssignmentDto>.Failure(scopeError);
        if (LineNotEditableCode(roster, assignment) is { } rosterError) return Result<RosterAssignmentDto>.Failure(rosterError);

        assignment.Note = NormalizeNote(request.Note);
        await rosterRepository.SaveChangesAsync(cancellationToken);
        return Result<RosterAssignmentDto>.Success(await GetAssignmentDtoAsync(roster.Id, assignment.Id, cancellationToken));
    }

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    /// <summary>Error code when the event's roster can no longer change, or null when it can.</summary>
    private static string? EventNotEditableCode(LiturgicalEvent liturgicalEvent)
    {
        if (liturgicalEvent.Status == EventStatus.Cancelled) return ErrorCodes.LiturgicalEventCancelled;

        return liturgicalEvent.EventDate < VietnamTime.Today ? ErrorCodes.LiturgicalEventAlreadyPassed : null;
    }

    private static string? RosterNotEditableCode(ServiceRoster roster) =>
        EventNotEditableCode(roster.LiturgicalEvent)
        ?? (roster.Status == RosterStatus.Finalized ? ErrorCodes.RosterAlreadyFinalized : null);

    /// <summary>Like <see cref="RosterNotEditableCode"/>, and a whole-service line is also locked once those lines were finalized early.</summary>
    private static string? LineNotEditableCode(ServiceRoster roster, RosterAssignment line) =>
        RosterNotEditableCode(roster)
        ?? (line.SongListItemId is null && roster.WholeServiceFinalizedAt is not null ? ErrorCodes.RosterAlreadyFinalized : null);

    /// <summary>
    /// Error code when the member cannot take the song / skill: already holds it on the roster (also when one line is
    /// whole-service and the other a song line of the same skill), missing, not Active,
    /// skill not approved or participation neither Confirmed nor Unsure. Null when they can.
    /// </summary>
    private async Task<string?> MemberNotAssignableCodeAsync(
        ServiceRoster? roster, Guid eventId, Guid? songListItemId, Guid skillId, Guid memberId, CancellationToken cancellationToken)
    {
        // Same song and skill, or the same skill across the whole service and a song: the organist already plays every song.
        if (roster?.Assignments.Any(a => a.Status == RosterAssignmentStatus.Active && a.SkillId == skillId && a.MemberId == memberId
                && (a.SongListItemId == songListItemId || a.SongListItemId is null || songListItemId is null)) == true)
            return ErrorCodes.RosterAssignmentDuplicate;

        var member = await rosterRepository.GetMemberForAssignmentAsync(memberId, skillId, eventId, cancellationToken);
        if (member is null) return ErrorCodes.MemberNotFound;

        // Only members of the choirs serving the event can serve it (D8); others read as not found.
        if (await choirScope.GetEventMemberIdsAsync(eventId, cancellationToken) is { } eventMemberIds
            && !eventMemberIds.Contains(memberId))
            return ErrorCodes.MemberNotFound;
        if (member.Status != MemberStatus.Active) return ErrorCodes.MemberNotActive;
        if (member.MemberSkills.Count == 0) return ErrorCodes.RosterAssignmentMemberSkillNotApproved;
        if (member.EventParticipations.Count == 0) return ErrorCodes.RosterAssignmentMemberNotConfirmed;
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
        LiturgicalEvent liturgicalEvent, List<Need> requirements, ServiceRoster? roster, CancellationToken cancellationToken)
    {
        var skillIds = requirements.Select(x => x.SkillId).Distinct().ToList();
        var candidateSkills = await rosterRepository.GetCandidateSkillsAsync(liturgicalEvent.Id, skillIds, cancellationToken);
        if (await choirScope.GetEventMemberIdsAsync(liturgicalEvent.Id, cancellationToken) is { } eventMemberIds)
            candidateSkills = candidateSkills.Where(x => eventMemberIds.Contains(x.MemberId)).ToList();

        var memberIds = candidateSkills.Select(x => x.MemberId).Distinct().ToList();
        var recentServiceDays = await systemSettingService.GetIntAsync(
            SystemSettingKeys.RosterRecentServiceDays, cancellationToken);
        var recent = memberIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await rosterRepository.CountRecentServicesAsync(
                memberIds, liturgicalEvent.EventDate.AddDays(-recentServiceDays), liturgicalEvent.EventDate, cancellationToken);

        var kept = roster?.Assignments
            .Where(x => x.Source == AssignmentSource.Manual || x.Status == RosterAssignmentStatus.Replaced)
            .ToList() ?? [];

        // Whole-service lines that survive this suggestion: their members already play that skill in every song.
        var wholeServiceHolders = roster?.Assignments
            .Where(a => a.Status == RosterAssignmentStatus.Active && a.SongListItemId is null
                && (a.Source == AssignmentSource.Manual || roster.WholeServiceFinalizedAt is not null))
            .ToList() ?? [];

        return requirements.Select((x, index) =>
        {
            var inSlot = kept.Where(a => a.SongListItemId == x.Item?.Id && a.SkillId == x.SkillId).ToList();
            var manualCount = inSlot.Count(a => a.Status == RosterAssignmentStatus.Active);
            var excludedMemberIds = inSlot.Select(a => a.MemberId)
                .Concat(x.Item is null ? [] : wholeServiceHolders.Where(a => a.SkillId == x.SkillId).Select(a => a.MemberId))
                .ToHashSet();

            var candidates = candidateSkills
                .Where(c => c.SkillId == x.SkillId && !excludedMemberIds.Contains(c.MemberId))
                .Select(c => new Candidate(c.MemberId, c.Level, recent.GetValueOrDefault(c.MemberId)))
                .OrderByDescending(c => c.Level.HasValue ? (int)c.Level.Value : -1)
                .ThenBy(c => c.RecentServiceCount)
                .ThenBy(c => c.MemberId)
                .ToList();

            return new Slot($"S{index + 1}", x, manualCount, candidates);
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

        // Switched off by the Admin: the caller then fills every slot by the built-in rules, as after an AI failure.
        if (!await systemSettingService.GetBoolAsync(SystemSettingKeys.RosterAiSuggestionEnabled, cancellationToken))
        {
            return false;
        }

        // Codes instead of ids, and no names or contact details, ever leave the server.
        var codes = open.SelectMany(x => x.Candidates).Select(x => x.MemberId).Distinct()
            .Select((id, index) => (Id: id, Code: $"M{index + 1}"))
            .ToList();
        var codeOf = codes.ToDictionary(x => x.Id, x => x.Code);
        var idOf = codes.ToDictionary(x => x.Code, x => x.Id);

        var input = open.Select(x => new RosterSlotInput
        {
            SlotCode = x.Code,
            SkillName = x.Need.Skill.Name,
            NeededCount = x.NeededCount,
            IsWholeService = x.Need.Item is null,
            Candidates = x.Candidates.Select(c => new RosterCandidateInput
            {
                MemberCode = codeOf[c.MemberId],
                Level = c.Level,
                RecentServiceCount = c.RecentServiceCount,
            }).ToList(),
        }).ToList();

        var result = await suggestionGenerator.GenerateAsync(input, cancellationToken);
        if (!result.IsSuccess) return false;

        ApplyAiPicks(open.ToDictionary(x => x.Code), result.Value!, idOf);
        DropWholeServiceRepeats(slots);
        return true;
    }

    /// <summary>Keeps only AI picks that are real candidates of their slot, not repeated, and within the needed count.</summary>
    private static void ApplyAiPicks(
        Dictionary<string, Slot> slotByCode, IEnumerable<RosterSlotPick> picks, Dictionary<string, Guid> idOf)
    {
        foreach (var pick in picks)
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
    }

    /// <summary>
    /// Tops every slot up from its remaining candidates. The whole fallback when the AI failed. Members holding fewer
    /// lines in this suggestion come first, best first among equals, so solos spread instead of piling on one singer.
    /// </summary>
    private static void FillByRules(List<Slot> slots)
    {
        var load = slots.SelectMany(x => x.Picked).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        // Whole-service slots come first in the list, so their picks are known before any song slot of the same skill.
        foreach (var slot in slots)
        {
            // OrderBy is stable: candidates with the same load keep their best-first order.
            foreach (var memberId in slot.Candidates.Select(c => c.MemberId).OrderBy(id => load.GetValueOrDefault(id)).ToList())
            {
                if (slot.Picked.Count >= slot.NeededCount) break;
                if (slot.Picked.Contains(memberId) || HoldsWholeService(slots, slot, memberId)) continue;

                slot.Picked.Add(memberId);
                load[memberId] = load.GetValueOrDefault(memberId) + 1;
            }
        }
    }

    /// <summary>True when a song slot's member already fills the same skill for the whole service: one person, not two.</summary>
    private static bool HoldsWholeService(List<Slot> slots, Slot slot, Guid memberId) =>
        slot.Need.Item is not null
        && slots.Any(x => x.Need.Item is null && x.Need.SkillId == slot.Need.SkillId && x.Picked.Contains(memberId));

    /// <summary>Removes AI picks that put a whole-service member on a song slot of the same skill; the rules refill them.</summary>
    private static void DropWholeServiceRepeats(List<Slot> slots)
    {
        foreach (var slot in slots)
            slot.Picked.RemoveAll(memberId => HoldsWholeService(slots, slot, memberId));
    }
}

using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class RosterServiceTests
{
    private readonly IServiceRosterRepository _repository = Substitute.For<IServiceRosterRepository>();
    private readonly IRosterSuggestionGenerator _generator = Substitute.For<IRosterSuggestionGenerator>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ISystemSettingService _settings = Substitute.For<ISystemSettingService>();
    private readonly RosterService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly Guid _directorId = Guid.NewGuid();
    private readonly Skill _soprano = new() { Id = Guid.NewGuid(), Name = "Soprano" };
    private readonly Skill _guitar = new() { Id = Guid.NewGuid(), Name = "Guitar" };
    private readonly Guid _an = Guid.NewGuid(), _binh = Guid.NewGuid(), _chi = Guid.NewGuid();

    private readonly LiturgicalEvent _event;
    private readonly SongList _songList;
    private readonly SongListItem _entrance;
    private List<RosterSlotInput> _sentToAi = [];

    public RosterServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<RosterAssignmentProfile>();
            cfg.AddProfile<ServiceRosterProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();
        _currentUser.UserId.Returns(_directorId);
        _sut = new RosterService(_repository, _generator, _currentUser, _notifications, _settings, ChoirScopeFakes.Unrestricted(), mapper);
        _settings.GetIntAsync(SystemSettingKeys.RosterRecentServiceDays, _ct).Returns(60);
        _settings.GetBoolAsync(SystemSettingKeys.RosterAiSuggestionEnabled, _ct).Returns(true);

        _event = new LiturgicalEvent
        {
            Id = Guid.NewGuid(),
            EventDate = VietnamTime.Today.AddDays(3),
            Status = EventStatus.Published,
        };
        _entrance = new SongListItem { Id = Guid.NewGuid(), Song = new Song { Title = "Nhap le" } };
        _entrance.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 2 });
        _entrance.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _guitar.Id, Skill = _guitar, RequiredCount = 1 });
        _songList = new SongList { EventId = _event.Id, Status = SongListStatus.Approved, Items = [_entrance] };

        _repository.GetEventWithRosterAsync(_event.Id, _ct).Returns(_event);
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns(_songList);
        _repository.GetCandidateSkillsAsync(_event.Id, Arg.Any<IReadOnlyCollection<Guid>>(), _ct).Returns(
        [
            new MemberSkill { MemberId = _an, SkillId = _soprano.Id, Level = SkillLevel.Beginner },
            new MemberSkill { MemberId = _binh, SkillId = _soprano.Id, Level = SkillLevel.Advanced },
            new MemberSkill { MemberId = _chi, SkillId = _soprano.Id, Level = SkillLevel.Advanced },
            new MemberSkill { MemberId = _an, SkillId = _guitar.Id, Level = SkillLevel.Intermediate },
        ]);
        // Chi served more recently than Binh, so the rules prefer Binh among the two Advanced sopranos.
        _repository.CountRecentServicesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), _ct)
            .Returns(new Dictionary<Guid, int> { [_chi] = 3, [_binh] = 1 });
        _repository.GetAssignmentsAsync(Arg.Any<Guid>(), _ct).Returns([]);
        _repository.GetMembersServingNearAsync(default!, default, default, default, default, default)
            .ReturnsForAnyArgs(new List<Guid>());

        AiFails();
    }

    /// <summary>The member holds a line on another event of the same date starting at <paramref name="start"/>.</summary>
    private void ServesAnotherEventAt(Guid memberId, TimeOnly start) =>
        _repository.GetMembersServingNearAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), _event.Id, _event.EventDate, Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), _ct)
            .Returns(call => call.ArgAt<IReadOnlyCollection<Guid>>(0).Contains(memberId)
                && call.ArgAt<TimeOnly>(3) <= start && start <= call.ArgAt<TimeOnly>(4)
                    ? new List<Guid> { memberId }
                    : new List<Guid>());

    private void AiFails() =>
        _generator.GenerateAsync(Arg.Do<List<RosterSlotInput>>(x => _sentToAi = x), _ct)
            .Returns(Result<List<RosterSlotPick>>.Failure(ErrorCodes.ExternalAiFailed));

    private void AiReturns(Func<List<RosterSlotInput>, List<RosterSlotPick>> picks) =>
        _generator.GenerateAsync(Arg.Do<List<RosterSlotInput>>(x => _sentToAi = x), _ct)
            .Returns(call => Result<List<RosterSlotPick>>.Success(picks(call.Arg<List<RosterSlotInput>>())));

    private ServiceRoster ExistingRoster(RosterStatus status = RosterStatus.Draft)
    {
        _event.ServiceRoster = new ServiceRoster { Id = Guid.NewGuid(), EventId = _event.Id, Status = status };
        return _event.ServiceRoster;
    }

    private static string CodeOf(List<RosterSlotInput> slots, string skillName, SkillLevel level, int recent) =>
        slots.First(s => s.SkillName == skillName).Candidates.First(c => c.Level == level && c.RecentServiceCount == recent).MemberCode;

    private Task<Result<RosterSuggestionResponse>> SuggestAsync() =>
        _sut.SuggestAsync(new SuggestServiceRosterRequest { EventId = _event.Id }, _ct);

    [Fact]
    public async Task Suggest_AiPicksValidMembers_SavesThemAsSuggested_Async()
    {
        var roster = ExistingRoster();
        // AI deliberately picks Chi over Binh; a valid choice must be kept as is.
        AiReturns(slots =>
        [
            new() { SlotCode = "S1", MemberCodes = [CodeOf(slots, "Soprano", SkillLevel.Advanced, 3), CodeOf(slots, "Soprano", SkillLevel.Beginner, 0)] },
            new() { SlotCode = "S2", MemberCodes = [CodeOf(slots, "Guitar", SkillLevel.Intermediate, 0)] },
        ]);

        var result = await SuggestAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsAiGenerated);
        Assert.Empty(result.Value.Shortages);
        Assert.Equal(RosterStatus.Suggested, roster.Status);
        Assert.Equal(_directorId, roster.GeneratedBy);
        Assert.All(roster.Assignments, x => Assert.Equal(AssignmentSource.Suggested, x.Source));
        Assert.Equal([_chi, _an], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
        Assert.Equal([_an], roster.Assignments.Where(x => x.SkillId == _guitar.Id).Select(x => x.MemberId));
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Suggest_SendsOnlyAnonymisedCodesToAi_Async()
    {
        ExistingRoster();

        await SuggestAsync();

        var codes = _sentToAi.SelectMany(x => x.Candidates).Select(x => x.MemberCode).Distinct().ToList();
        Assert.Equal(3, codes.Count);
        Assert.All(codes, x => Assert.Matches("^M[0-9]+$", x));
    }

    [Fact]
    public async Task Suggest_AiReturnsInvalidPicks_DropsThemAndFillsByRules_Async()
    {
        var roster = ExistingRoster();
        AiReturns(slots =>
        [
            // Unknown code and a repeat in the soprano slot.
            new() { SlotCode = "S1", MemberCodes = ["M99", CodeOf(slots, "Soprano", SkillLevel.Beginner, 0), CodeOf(slots, "Soprano", SkillLevel.Beginner, 0)] },
            // Chi does not play guitar, and the guitar slot needs only one person.
            new() { SlotCode = "S2", MemberCodes = [CodeOf(slots, "Soprano", SkillLevel.Advanced, 3), CodeOf(slots, "Guitar", SkillLevel.Intermediate, 0), "M1", "M2"] },
            new() { SlotCode = "S9", MemberCodes = [CodeOf(slots, "Soprano", SkillLevel.Advanced, 1)] },
        ]);

        var result = await SuggestAsync();

        Assert.True(result.Value!.IsAiGenerated);
        // An kept from the AI, then the best remaining soprano (Binh: Advanced, fewer recent services).
        Assert.Equal([_an, _binh], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
        Assert.Equal([_an], roster.Assignments.Where(x => x.SkillId == _guitar.Id).Select(x => x.MemberId));
    }

    [Fact]
    public async Task Suggest_AiSwitchedOffInSettings_FillsByRulesWithoutCallingTheAi_Async()
    {
        var roster = ExistingRoster();
        _settings.GetBoolAsync(SystemSettingKeys.RosterAiSuggestionEnabled, _ct).Returns(false);

        var result = await SuggestAsync();

        Assert.False(result.Value!.IsAiGenerated);
        Assert.Equal([_binh, _chi], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default);
    }

    [Fact]
    public async Task Suggest_CountsRecentServicesOverTheConfiguredDays_Async()
    {
        ExistingRoster();
        _settings.GetIntAsync(SystemSettingKeys.RosterRecentServiceDays, _ct).Returns(30);

        await SuggestAsync();

        await _repository.Received(1).CountRecentServicesAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), _event.EventDate.AddDays(-30), _event.EventDate, _ct);
    }

    [Fact]
    public async Task Suggest_AiFails_FallsBackToRules_Async()
    {
        var roster = ExistingRoster();

        var result = await SuggestAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsAiGenerated);
        Assert.Equal([_binh, _chi], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
    }

    [Fact]
    public async Task Suggest_NotEnoughCandidates_ReturnsShortage_Async()
    {
        ExistingRoster();
        _entrance.PersonnelRequirements.First(x => x.SkillId == _guitar.Id).RequiredCount = 3;

        var result = await SuggestAsync();

        var shortage = Assert.Single(result.Value!.Shortages);
        Assert.Equal(_guitar.Id, shortage.SkillId);
        Assert.Equal("Nhap le", shortage.SongTitle);
        Assert.Equal(3, shortage.RequiredCount);
        Assert.Equal(1, shortage.AssignedCount);
    }

    [Fact]
    public async Task Suggest_MemberServingAnotherEventNearby_IsNotPicked_Async()
    {
        var roster = ExistingRoster();
        ServesAnotherEventAt(_binh, _event.Time);

        var result = await SuggestAsync();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(_binh, roster.Assignments.Select(x => x.MemberId));
        Assert.Equal([_chi, _an], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
    }

    [Fact]
    public async Task Suggest_ReplacesOldSuggestionsAndKeepsManualOnes_Async()
    {
        var roster = ExistingRoster(RosterStatus.Suggested);
        var manual = new RosterAssignment { MemberId = _chi, SkillId = _soprano.Id, SongListItemId = _entrance.Id, Source = AssignmentSource.Manual };
        var oldSuggestion = new RosterAssignment { MemberId = Guid.NewGuid(), SkillId = _guitar.Id, SongListItemId = _entrance.Id, Source = AssignmentSource.Suggested };
        roster.Assignments.Add(manual);
        roster.Assignments.Add(oldSuggestion);

        await SuggestAsync();

        Assert.Contains(manual, roster.Assignments);
        Assert.DoesNotContain(oldSuggestion, roster.Assignments);
        // Chi already fills one soprano place by hand, so only one more is picked and Chi is not a candidate again.
        Assert.Equal([_chi, _binh], roster.Assignments.Where(x => x.SkillId == _soprano.Id).Select(x => x.MemberId));
        Assert.DoesNotContain(_sentToAi.First(x => x.SkillName == "Soprano").Candidates, c => c.RecentServiceCount == 3);
    }

    [Fact]
    public async Task Suggest_NoRosterYet_CreatesOne_Async()
    {
        ServiceRoster? added = null;
        await _repository.AddAsync(Arg.Do<ServiceRoster>(x => added = x), _ct);

        var result = await SuggestAsync();

        Assert.NotNull(added);
        Assert.Equal(_event.Id, added.EventId);
        Assert.Equal(added.Id, result.Value!.RosterId);
        Assert.Equal(3, added.Assignments.Count);
    }

    [Fact]
    public async Task Suggest_MissingEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.SuggestAsync(new SuggestServiceRosterRequest { EventId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Fact]
    public async Task Suggest_CancelledEvent_ReturnsEventCancelled_Async()
    {
        _event.Status = EventStatus.Cancelled;

        Assert.Equal(ErrorCodes.LiturgicalEventCancelled, (await SuggestAsync()).Code);
    }

    [Fact]
    public async Task Suggest_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, (await SuggestAsync()).Code);
    }

    [Fact]
    public async Task Suggest_NoApprovedSongList_ReturnsSongListNotApproved_Async()
    {
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        Assert.Equal(ErrorCodes.RosterSongListNotApproved, (await SuggestAsync()).Code);
    }

    [Fact]
    public async Task Suggest_NoPersonnelRequirement_ReturnsNoPersonnelRequirement_Async()
    {
        _entrance.PersonnelRequirements.Clear();

        Assert.Equal(ErrorCodes.RosterNoPersonnelRequirement, (await SuggestAsync()).Code);
    }

    [Fact]
    public async Task Suggest_FinalizedRoster_ReturnsRosterAlreadyFinalized_Async()
    {
        ExistingRoster(RosterStatus.Finalized);

        var result = await SuggestAsync();

        Assert.Equal(ErrorCodes.RosterAlreadyFinalized, result.Code);
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, _ct);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task GetShortages_PartlyStaffedRoster_ReturnsOnlyUnfilledRequirements_Async()
    {
        var roster = ExistingRoster(RosterStatus.Suggested);
        roster.Assignments.Add(new RosterAssignment { MemberId = _binh, SkillId = _soprano.Id, SongListItemId = _entrance.Id });
        roster.Assignments.Add(new RosterAssignment { MemberId = _an, SkillId = _guitar.Id, SongListItemId = _entrance.Id });

        var result = await _sut.GetShortagesAsync(_event.Id, _ct);

        var shortage = Assert.Single(result.Value!);
        Assert.Equal(_soprano.Id, shortage.SkillId);
        Assert.Equal(2, shortage.RequiredCount);
        Assert.Equal(1, shortage.AssignedCount);
    }

    [Fact]
    public async Task GetShortages_NoRosterYet_ReturnsEveryRequirement_Async()
    {
        var result = await _sut.GetShortagesAsync(_event.Id, _ct);

        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, x => Assert.Equal(0, x.AssignedCount));
    }

    [Fact]
    public async Task GetShortages_MissingEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.GetShortagesAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Fact]
    public async Task GetShortages_NoApprovedSongList_ReturnsSongListNotApproved_Async()
    {
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        Assert.Equal(ErrorCodes.RosterSongListNotApproved, (await _sut.GetShortagesAsync(_event.Id, _ct)).Code);
    }

    [Fact]
    public async Task GetByEvent_SavedRoster_ReturnsAssignmentsAndShortages_Async()
    {
        var roster = ExistingRoster(RosterStatus.Suggested);
        _repository.GetAssignmentsAsync(roster.Id, _ct).Returns(
        [
            new RosterAssignment { Id = Guid.NewGuid(), MemberId = _an, SkillId = _guitar.Id, SongListItemId = _entrance.Id },
        ]);

        var result = await _sut.GetByEventAsync(_event.Id, _ct);

        Assert.Equal(roster.Id, result.Value!.Id);
        Assert.Equal(RosterStatus.Suggested, result.Value.Status);
        Assert.Single(result.Value.Assignments);
        Assert.Equal(_soprano.Id, Assert.Single(result.Value.Shortages).SkillId);
    }

    [Fact]
    public async Task GetByEvent_NoRosterYet_ReturnsRosterNotFound_Async()
    {
        Assert.Equal(ErrorCodes.RosterNotFound, (await _sut.GetByEventAsync(_event.Id, _ct)).Code);
    }

    [Fact]
    public async Task GetByEvent_NoApprovedSongList_ReturnsRosterWithoutShortages_Async()
    {
        ExistingRoster();
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        var result = await _sut.GetByEventAsync(_event.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Shortages);
    }

    [Fact]
    public async Task Suggest_KeepsReplacedLinesAndDoesNotPickReplacedMemberAgain_Async()
    {
        var roster = ExistingRoster(RosterStatus.Suggested);
        var replaced = new RosterAssignment
        {
            MemberId = _binh, SkillId = _soprano.Id, SongListItemId = _entrance.Id,
            Source = AssignmentSource.Suggested, Status = RosterAssignmentStatus.Replaced,
        };
        roster.Assignments.Add(replaced);

        await SuggestAsync();

        Assert.Contains(replaced, roster.Assignments);
        Assert.DoesNotContain(_sentToAi.First(x => x.SkillName == "Soprano").Candidates, c => c.RecentServiceCount == 1);
        Assert.Equal([_chi, _an], roster.Assignments
            .Where(x => x.SkillId == _soprano.Id && x.Status == RosterAssignmentStatus.Active).Select(x => x.MemberId));
    }

    // --- Manual adjustment (UC-25b) ---

    private void MemberIs(Guid memberId, bool skillApproved = true, bool confirmed = true, MemberStatus status = MemberStatus.Active)
    {
        var member = new MemberProfile { Id = memberId, Status = status };
        if (skillApproved) member.MemberSkills.Add(new MemberSkill { MemberId = memberId, SkillId = _soprano.Id });
        if (confirmed) member.EventParticipations.Add(new EventParticipation { MemberId = memberId, EventId = _event.Id });
        _repository.GetMemberForAssignmentAsync(memberId, Arg.Any<Guid>(), _event.Id, _ct).Returns(member);
    }

    /// <summary>Existing roster, reachable by assignment id like the repository does, returning its Active lines as DTO source.</summary>
    private ServiceRoster RosterWith(RosterStatus status, params RosterAssignment[] assignments)
    {
        var roster = ExistingRoster(status);
        roster.LiturgicalEvent = _event;
        foreach (var a in assignments)
        {
            a.Id = a.Id == Guid.Empty ? Guid.NewGuid() : a.Id;
            a.RosterId = roster.Id;
            roster.Assignments.Add(a);
            _repository.GetRosterByAssignmentAsync(a.Id, _ct).Returns(roster);
        }

        _repository.GetAssignmentsAsync(roster.Id, _ct)
            .Returns(_ => roster.Assignments.Where(x => x.Status == RosterAssignmentStatus.Active).ToList());
        return roster;
    }

    private RosterAssignment SopranoLine(Guid memberId) =>
        new() { MemberId = memberId, SkillId = _soprano.Id, SongListItemId = _entrance.Id, Source = AssignmentSource.Suggested };

    private Task<Result<RosterAssignmentDto>> AddAsync(Guid memberId, Guid? skillId = null) =>
        _sut.AddAssignmentAsync(new CreateRosterAssignmentRequest
        {
            EventId = _event.Id, SongListItemId = _entrance.Id, SkillId = skillId ?? _soprano.Id, MemberId = memberId,
        }, _ct);

    [Fact]
    public async Task AddAssignment_EligibleMember_AddsManualLine_Async()
    {
        var roster = RosterWith(RosterStatus.Suggested);
        MemberIs(_an);

        var result = await AddAsync(_an);

        var line = Assert.Single(roster.Assignments);
        Assert.Equal(AssignmentSource.Manual, line.Source);
        Assert.Equal(RosterAssignmentStatus.Active, line.Status);
        Assert.Equal(line.Id, result.Value!.Id);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task AddAssignment_BeyondRequiredCount_IsAllowed_Async()
    {
        RosterWith(RosterStatus.Suggested, SopranoLine(_binh), SopranoLine(_chi));
        MemberIs(_an);

        Assert.True((await AddAsync(_an)).IsSuccess);
    }

    [Fact]
    public async Task AddAssignment_NoRosterYet_CreatesDraftRoster_Async()
    {
        ServiceRoster? added = null;
        await _repository.AddAsync(Arg.Do<ServiceRoster>(x => added = x), _ct);
        _repository.GetAssignmentsAsync(Arg.Any<Guid>(), _ct).Returns(_ => added!.Assignments.ToList());
        MemberIs(_an);

        var result = await AddAsync(_an);

        Assert.True(result.IsSuccess);
        Assert.Equal(RosterStatus.Draft, added!.Status);
        Assert.Single(added.Assignments);
    }

    [Fact]
    public async Task AddAssignment_SkillNotRequiredBySong_ReturnsPersonnelRequirementNotFound_Async()
    {
        MemberIs(_an);

        Assert.Equal(ErrorCodes.PersonnelRequirementNotFound, (await AddAsync(_an, Guid.NewGuid())).Code);
    }

    [Fact]
    public async Task AddAssignment_SkillNotApproved_ReturnsSkillNotApproved_Async()
    {
        MemberIs(_an, skillApproved: false);

        Assert.Equal(ErrorCodes.RosterAssignmentMemberSkillNotApproved, (await AddAsync(_an)).Code);
    }

    [Fact]
    public async Task AddAssignment_ParticipationNotConfirmed_ReturnsNotConfirmed_Async()
    {
        MemberIs(_an, confirmed: false);

        Assert.Equal(ErrorCodes.RosterAssignmentMemberNotConfirmed, (await AddAsync(_an)).Code);
    }

    [Fact]
    public async Task AddAssignment_MemberAlreadyHoldsSlot_ReturnsDuplicate_Async()
    {
        RosterWith(RosterStatus.Suggested, SopranoLine(_an));
        MemberIs(_an);

        Assert.Equal(ErrorCodes.RosterAssignmentDuplicate, (await AddAsync(_an)).Code);
    }

    [Theory]
    [InlineData(7, 120, false)] // exactly the window apart: two Masses back to back are fine
    [InlineData(7, 121, true)]
    [InlineData(9, 0, true)] // 0 still refuses the same start
    [InlineData(8, 0, false)]
    public async Task AddAssignment_MemberServingAnotherEventNearby_ReturnsTimeConflict_Async(
        int otherStartHour, int overlapMinutes, bool conflict)
    {
        _event.Time = new TimeOnly(9, 0);
        _settings.GetIntAsync(SystemSettingKeys.RosterEventOverlapMinutes, _ct).Returns(overlapMinutes);
        RosterWith(RosterStatus.Suggested);
        ServesAnotherEventAt(_an, new TimeOnly(otherStartHour, 0));
        MemberIs(_an);

        var result = await AddAsync(_an);

        Assert.Equal(conflict ? ErrorCodes.RosterMemberTimeConflict : null, result.Code);
    }

    [Fact]
    public async Task AddAssignment_FinalizedRoster_ReturnsRosterAlreadyFinalized_Async()
    {
        RosterWith(RosterStatus.Finalized);
        MemberIs(_an);

        Assert.Equal(ErrorCodes.RosterAlreadyFinalized, (await AddAsync(_an)).Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task AddAssignment_MissingEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.AddAssignmentAsync(new CreateRosterAssignmentRequest { EventId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }

    [Fact]
    public async Task ReplaceAssignment_KeepsOldLineAsReplacedAndLinksNewOne_Async()
    {
        var old = SopranoLine(_binh);
        var roster = RosterWith(RosterStatus.Suggested, old);
        MemberIs(_an);

        var result = await _sut.ReplaceAssignmentAsync(old.Id, new ReplaceRosterAssignmentRequest { MemberId = _an }, _ct);

        var replacement = Assert.Single(roster.Assignments, x => x.Status == RosterAssignmentStatus.Active);
        Assert.Equal(_an, replacement.MemberId);
        Assert.Equal(AssignmentSource.Manual, replacement.Source);
        Assert.Equal((old.SkillId, old.SongListItemId), (replacement.SkillId, replacement.SongListItemId));
        Assert.Equal(RosterAssignmentStatus.Replaced, old.Status);
        Assert.Equal(replacement.Id, old.ReplacedByAssignmentId);
        Assert.Equal(replacement.Id, result.Value!.Id);
    }

    [Fact]
    public async Task ReplaceAssignment_MemberServingAnotherEventNearby_ReturnsTimeConflictAndKeepsOldLine_Async()
    {
        var old = SopranoLine(_binh);
        RosterWith(RosterStatus.Suggested, old);
        ServesAnotherEventAt(_an, _event.Time);
        MemberIs(_an);

        var result = await _sut.ReplaceAssignmentAsync(old.Id, new ReplaceRosterAssignmentRequest { MemberId = _an }, _ct);

        Assert.Equal(ErrorCodes.RosterMemberTimeConflict, result.Code);
        Assert.Equal(RosterAssignmentStatus.Active, old.Status);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task ReplaceAssignment_AlreadyReplacedLine_ReturnsAssignmentNotFound_Async()
    {
        var old = SopranoLine(_binh);
        old.Status = RosterAssignmentStatus.Replaced;
        RosterWith(RosterStatus.Suggested, old);
        MemberIs(_an);

        var result = await _sut.ReplaceAssignmentAsync(old.Id, new ReplaceRosterAssignmentRequest { MemberId = _an }, _ct);

        Assert.Equal(ErrorCodes.RosterAssignmentNotFound, result.Code);
    }

    [Fact]
    public async Task ReplaceAssignment_InactiveMember_ReturnsMemberNotActive_Async()
    {
        var old = SopranoLine(_binh);
        RosterWith(RosterStatus.Suggested, old);
        MemberIs(_an, status: MemberStatus.Inactive);

        var result = await _sut.ReplaceAssignmentAsync(old.Id, new ReplaceRosterAssignmentRequest { MemberId = _an }, _ct);

        Assert.Equal(ErrorCodes.MemberNotActive, result.Code);
        Assert.Equal(RosterAssignmentStatus.Active, old.Status);
    }

    [Fact]
    public async Task RemoveAssignment_ActiveLine_RemovesIt_Async()
    {
        var line = SopranoLine(_binh);
        var roster = RosterWith(RosterStatus.Suggested, line);

        var result = await _sut.RemoveAssignmentAsync(line.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(roster.Assignments);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task RemoveAssignment_Missing_ReturnsAssignmentNotFound_Async()
    {
        Assert.Equal(ErrorCodes.RosterAssignmentNotFound, (await _sut.RemoveAssignmentAsync(Guid.NewGuid(), _ct)).Code);
    }

    [Fact]
    public async Task RemoveAssignment_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        var line = SopranoLine(_binh);
        RosterWith(RosterStatus.Suggested, line);
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, (await _sut.RemoveAssignmentAsync(line.Id, _ct)).Code);
    }

    // --- Finalize (UC-26) ---

    /// <summary>Soprano line whose member is loaded the way GetAssignmentsWithEligibilityAsync loads it.</summary>
    private RosterAssignment SopranoLineFor(
        Guid memberId, MemberStatus status = MemberStatus.Active, bool skillApproved = true, bool confirmed = true)
    {
        var member = new MemberProfile { Id = memberId, Status = status, User = new User { FullName = "Member" } };
        if (skillApproved) member.MemberSkills.Add(new MemberSkill { MemberId = memberId, SkillId = _soprano.Id });
        if (confirmed) member.EventParticipations.Add(new EventParticipation { MemberId = memberId, EventId = _event.Id });

        var line = SopranoLine(memberId);
        line.Id = Guid.NewGuid();
        line.Member = member;
        line.Skill = _soprano;
        return line;
    }

    private ServiceRoster FinalizableRoster(RosterStatus status, params RosterAssignment[] lines)
    {
        var roster = ExistingRoster(status);
        roster.LiturgicalEvent = _event;
        _repository.GetRosterWithEventAsync(roster.Id, _ct).Returns(roster);
        _repository.GetAssignmentsWithEligibilityAsync(roster.Id, _event.Id, _ct).Returns(lines.ToList());
        return roster;
    }

    [Fact]
    public async Task Finalize_EligibleLinesWithShortage_LocksRosterAndReturnsShortage_Async()
    {
        var roster = FinalizableRoster(RosterStatus.Suggested, SopranoLineFor(_an), SopranoLineFor(_binh));

        var result = await _sut.FinalizeAsync(roster.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(RosterStatus.Finalized, roster.Status);
        Assert.Equal(_directorId, roster.FinalizedBy);
        Assert.NotNull(roster.FinalizedAt);
        Assert.Equal(RosterStatus.Finalized, result.Value!.Status);
        Assert.Equal(2, result.Value.Assignments.Count);
        var shortage = Assert.Single(result.Value.Shortages);
        Assert.Equal(_guitar.Id, shortage.SkillId);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Finalize_MissingRoster_ReturnsRosterNotFound_Async()
    {
        Assert.Equal(ErrorCodes.RosterNotFound, (await _sut.FinalizeAsync(Guid.NewGuid(), _ct)).Code);
    }

    [Fact]
    public async Task Finalize_AlreadyFinalized_ReturnsRosterAlreadyFinalized_Async()
    {
        var roster = FinalizableRoster(RosterStatus.Finalized, SopranoLineFor(_an));

        Assert.Equal(ErrorCodes.RosterAlreadyFinalized, (await _sut.FinalizeAsync(roster.Id, _ct)).Code);
    }

    [Fact]
    public async Task Finalize_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        var roster = FinalizableRoster(RosterStatus.Suggested, SopranoLineFor(_an));
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, (await _sut.FinalizeAsync(roster.Id, _ct)).Code);
    }

    [Fact]
    public async Task Finalize_NoApprovedSongList_ReturnsSongListNotApproved_Async()
    {
        var roster = FinalizableRoster(RosterStatus.Suggested, SopranoLineFor(_an));
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        Assert.Equal(ErrorCodes.RosterSongListNotApproved, (await _sut.FinalizeAsync(roster.Id, _ct)).Code);
    }

    [Theory]
    [InlineData(MemberStatus.Inactive, true, true, ErrorCodes.MemberNotActive)]
    [InlineData(MemberStatus.Active, false, true, ErrorCodes.RosterAssignmentMemberSkillNotApproved)]
    [InlineData(MemberStatus.Active, true, false, ErrorCodes.RosterAssignmentMemberNotConfirmed)]
    public async Task Finalize_LineNoLongerEligible_ReturnsItsCodeAndKeepsRosterOpen_Async(
        MemberStatus status, bool skillApproved, bool confirmed, string expectedCode)
    {
        var invalid = SopranoLineFor(_binh, status, skillApproved, confirmed);
        var roster = FinalizableRoster(RosterStatus.Suggested, SopranoLineFor(_an), invalid);

        var result = await _sut.FinalizeAsync(roster.Id, _ct);

        Assert.Equal(expectedCode, result.Code);
        Assert.Contains(invalid.Id.ToString(), result.Message);
        Assert.Equal(RosterStatus.Suggested, roster.Status);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }

    // --- Assignment notifications (UC-27) ---

    /// <summary>Soprano line on the entrance song, loaded the way GetRosterForNotificationAsync loads it.</summary>
    private RosterAssignment NotifiableLine(Guid memberId, DateTime? notifiedAt = null)
    {
        var line = SopranoLineFor(memberId);
        line.Member.UserId = Guid.NewGuid();
        line.SongListItem = _entrance;
        line.NotifiedAt = notifiedAt;
        return line;
    }

    private ServiceRoster NotifiableRoster(RosterStatus status, params RosterAssignment[] lines)
    {
        var roster = ExistingRoster(status);
        roster.LiturgicalEvent = _event;
        foreach (var line in lines) roster.Assignments.Add(line);
        _repository.GetRosterForNotificationAsync(roster.Id, _ct).Returns(roster);
        return roster;
    }

    private Task<Result> NotifyAsync(ServiceRoster roster, params Guid[] memberIds) =>
        _sut.SendNotificationsAsync(roster.Id, new SendRosterNotificationsRequest { MemberIds = [.. memberIds] }, _ct);

    [Fact]
    public async Task SendNotifications_SelectedMember_NotifiesOnlyThemAndStampsTheirLines_Async()
    {
        var an = NotifiableLine(_an);
        var binh = NotifiableLine(_binh);
        var roster = NotifiableRoster(RosterStatus.Finalized, an, binh);

        var result = await NotifyAsync(roster, _an);

        Assert.True(result.IsSuccess);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.Type == NotificationType.AssignmentNotice
                && x.RecipientUserIds.Single() == an.Member.UserId
                && x.Content.Contains("Soprano - Nhap le")
                && x.ReferenceId == _event.Id),
            _ct);
        Assert.NotNull(an.NotifiedAt);
        Assert.Null(binh.NotifiedAt);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task SendNotifications_NoSelection_NotifiesOnlyMembersNotNotifiedYet_Async()
    {
        var an = NotifiableLine(_an, notifiedAt: DateTime.UtcNow.AddDays(-1));
        var binh = NotifiableLine(_binh);
        var roster = NotifiableRoster(RosterStatus.Finalized, an, binh);

        var result = await NotifyAsync(roster);

        Assert.True(result.IsSuccess);
        await _notifications.Received(1).SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.RecipientUserIds.Single() == binh.Member.UserId), _ct);
        Assert.NotNull(binh.NotifiedAt);
    }

    [Fact]
    public async Task SendNotifications_RosterNotFinalized_ReturnsRosterNotFinalized_Async()
    {
        var roster = NotifiableRoster(RosterStatus.Suggested, NotifiableLine(_an));

        Assert.Equal(ErrorCodes.RosterNotFinalized, (await NotifyAsync(roster, _an)).Code);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task SendNotifications_MissingRoster_ReturnsRosterNotFound_Async()
    {
        var result = await _sut.SendNotificationsAsync(Guid.NewGuid(), new SendRosterNotificationsRequest(), _ct);

        Assert.Equal(ErrorCodes.RosterNotFound, result.Code);
    }

    [Fact]
    public async Task SendNotifications_MemberNotOnRoster_ReturnsAssignmentNotFoundAndSendsNothing_Async()
    {
        var roster = NotifiableRoster(RosterStatus.Finalized, NotifiableLine(_an));

        Assert.Equal(ErrorCodes.RosterAssignmentNotFound, (await NotifyAsync(roster, _an, _chi)).Code);
        await _notifications.DidNotReceive().SendAsync(Arg.Any<SendNotificationRequest>(), _ct);
    }

    [Fact]
    public async Task SendNotifications_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        var roster = NotifiableRoster(RosterStatus.Finalized, NotifiableLine(_an));
        _event.EventDate = VietnamTime.Today.AddDays(-1);

        Assert.Equal(ErrorCodes.LiturgicalEventAlreadyPassed, (await NotifyAsync(roster, _an)).Code);
    }

    // ---- Whole-service requirements (organist, conductor...) ----

    private void EventNeedsGuitar() =>
        _event.PersonnelRequirements.Add(new EventPersonnelRequirement { SkillId = _guitar.Id, Skill = _guitar, RequiredCount = 1 });

    [Fact]
    public async Task Suggest_WholeServiceRequirementWithoutApprovedSongList_StaffsItWithoutASong_Async()
    {
        var roster = ExistingRoster();
        EventNeedsGuitar();
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        var result = await SuggestAsync();

        Assert.True(result.IsSuccess);
        var line = Assert.Single(roster.Assignments);
        Assert.Equal((_an, _guitar.Id, (Guid?)null), (line.MemberId, line.SkillId, line.SongListItemId));
    }

    [Fact]
    public async Task Suggest_SongRequirementOfAWholeServiceSkill_IsCoveredByTheWholeServiceLine_Async()
    {
        var roster = ExistingRoster();
        EventNeedsGuitar();

        var result = await SuggestAsync();

        // The entrance song also asks for a guitar, but the whole-service guitarist plays it.
        Assert.Null(Assert.Single(roster.Assignments, x => x.SkillId == _guitar.Id).SongListItemId);
        Assert.DoesNotContain(result.Value!.Shortages, x => x.SkillId == _guitar.Id);
    }

    [Fact]
    public async Task Suggest_ByRules_SpreadsSongLinesAcrossMembers_Async()
    {
        var roster = ExistingRoster();
        _entrance.PersonnelRequirements.Remove(_entrance.PersonnelRequirements.Single(x => x.SkillId == _guitar.Id));
        var offertory = new SongListItem { Id = Guid.NewGuid(), DisplayOrder = 1, Song = new Song { Title = "Dang le" } };
        offertory.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 1 });
        _songList.Items.Add(offertory);

        await SuggestAsync();

        // Binh and Chi already sing the entrance, so the offertory goes to An despite a lower level.
        Assert.Equal(_an, Assert.Single(roster.Assignments, x => x.SongListItemId == offertory.Id).MemberId);
    }

    [Fact]
    public async Task GetShortages_NoApprovedSongListButWholeServiceRequirement_ReturnsItsShortage_Async()
    {
        EventNeedsGuitar();
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        var result = await _sut.GetShortagesAsync(_event.Id, _ct);

        var shortage = Assert.Single(result.Value!);
        Assert.Equal((_guitar.Id, (Guid?)null, 0), (shortage.SkillId, shortage.SongListItemId, shortage.AssignedCount));
    }

    [Fact]
    public async Task AddAssignment_WholeServiceLine_NeedsEventRequirementAndKeepsNote_Async()
    {
        var roster = RosterWith(RosterStatus.Suggested);
        MemberIs(_an);
        var request = new CreateRosterAssignmentRequest { EventId = _event.Id, SkillId = _guitar.Id, MemberId = _an, Note = "  intro  " };

        var missing = await _sut.AddAssignmentAsync(request, _ct);
        EventNeedsGuitar();
        var added = await _sut.AddAssignmentAsync(request, _ct);

        Assert.Equal(ErrorCodes.PersonnelRequirementNotFound, missing.Code);
        Assert.True(added.IsSuccess);
        var line = Assert.Single(roster.Assignments);
        Assert.Equal(((Guid?)null, "intro"), (line.SongListItemId, line.Note));
    }

    [Fact]
    public async Task UpdateNote_And_Replace_KeepOrClearTheRoleNote_Async()
    {
        var line = SopranoLine(_an);
        var roster = RosterWith(RosterStatus.Suggested, line);
        MemberIs(_binh);

        await _sut.UpdateNoteAsync(line.Id, new UpdateRosterAssignmentNoteRequest { Note = "Narrator" }, _ct);
        await _sut.ReplaceAssignmentAsync(line.Id, new ReplaceRosterAssignmentRequest { MemberId = _binh }, _ct);
        var replacement = roster.Assignments.Single(x => x.Status == RosterAssignmentStatus.Active);
        var keptNote = replacement.Note;
        _repository.GetRosterByAssignmentAsync(replacement.Id, _ct).Returns(roster);
        await _sut.UpdateNoteAsync(replacement.Id, new UpdateRosterAssignmentNoteRequest { Note = " " }, _ct);

        Assert.Equal(("Narrator", "Narrator"), (line.Note, keptNote));
        Assert.Null(replacement.Note);
    }

    // ---- Whole-service lines finalized ahead of the song list ----

    private RosterAssignment GuitarWholeServiceLine(Guid memberId)
    {
        var line = SopranoLineFor(memberId);
        line.SkillId = _guitar.Id;
        line.Skill = _guitar;
        line.SongListItemId = null;
        line.Member.MemberSkills.Add(new MemberSkill { MemberId = memberId, SkillId = _guitar.Id });
        return line;
    }

    [Fact]
    public async Task FinalizeWholeService_ChecksOnlyWholeServiceLines_ThenLocksThem_Async()
    {
        EventNeedsGuitar();
        var guitarist = GuitarWholeServiceLine(_an);
        var roster = FinalizableRoster(RosterStatus.Suggested, guitarist, SopranoLineFor(_binh, confirmed: false));
        roster.Assignments.Add(guitarist);
        _repository.GetRosterByAssignmentAsync(guitarist.Id, _ct).Returns(roster);

        var result = await _sut.FinalizeWholeServiceAsync(roster.Id, _ct);
        var again = await _sut.FinalizeWholeServiceAsync(roster.Id, _ct);
        var remove = await _sut.RemoveAssignmentAsync(guitarist.Id, _ct);

        // Binh's song line is no longer valid, but only the whole-service lines are checked at this step.
        Assert.True(result.IsSuccess);
        Assert.NotNull(roster.WholeServiceFinalizedAt);
        Assert.Equal(RosterStatus.Suggested, roster.Status);
        Assert.Equal([ErrorCodes.RosterAlreadyFinalized, ErrorCodes.RosterAlreadyFinalized], new[] { again.Code, remove.Code });
    }

    [Fact]
    public async Task FinalizeWholeService_NoWholeServiceRequirement_ReturnsNoPersonnelRequirement_Async()
    {
        var roster = FinalizableRoster(RosterStatus.Suggested);

        Assert.Equal(ErrorCodes.RosterNoPersonnelRequirement, (await _sut.FinalizeWholeServiceAsync(roster.Id, _ct)).Code);
    }

    [Fact]
    public async Task Suggest_AfterWholeServiceFinalized_KeepsTheLockedLineAndPicksNoOther_Async()
    {
        EventNeedsGuitar();
        var roster = ExistingRoster();
        roster.WholeServiceFinalizedAt = DateTime.UtcNow;
        var locked = new RosterAssignment { Id = Guid.NewGuid(), MemberId = _chi, SkillId = _guitar.Id, Source = AssignmentSource.Suggested };
        roster.Assignments.Add(locked);

        await SuggestAsync();

        Assert.Same(locked, Assert.Single(roster.Assignments, x => x.SkillId == _guitar.Id));
    }

    [Fact]
    public async Task SendNotifications_OnlyWholeServiceFinalized_NotifiesOnlyWholeServiceLines_Async()
    {
        var guitarist = NotifiableLine(_an);
        guitarist.SongListItemId = null;
        guitarist.SongListItem = null;
        guitarist.SkillId = _guitar.Id;
        guitarist.Skill = _guitar;
        var soprano = NotifiableLine(_binh);
        var roster = NotifiableRoster(RosterStatus.Suggested, guitarist, soprano);
        var beforeLock = await NotifyAsync(roster);
        roster.WholeServiceFinalizedAt = DateTime.UtcNow;

        var result = await NotifyAsync(roster);

        Assert.Equal(ErrorCodes.RosterNotFinalized, beforeLock.Code);
        Assert.True(result.IsSuccess);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(x => x.RecipientUserIds.Single() == guitarist.Member.UserId && x.Content.Contains("Guitar (cả buổi lễ)")),
            _ct);
        Assert.Null(soprano.NotifiedAt);
    }

    // ---- Song needing more of a whole-service skill ----

    [Fact]
    public async Task Suggest_SongNeedsMoreThanWholeService_StaffsOnlyTheRestWithAnotherMember_Async()
    {
        var roster = ExistingRoster();
        EventNeedsGuitar();
        _entrance.PersonnelRequirements.Single(x => x.SkillId == _guitar.Id).RequiredCount = 2;
        _repository.GetCandidateSkillsAsync(_event.Id, Arg.Any<IReadOnlyCollection<Guid>>(), _ct).Returns(
        [
            new MemberSkill { MemberId = _an, SkillId = _guitar.Id, Level = SkillLevel.Advanced },
            new MemberSkill { MemberId = _binh, SkillId = _guitar.Id, Level = SkillLevel.Beginner },
        ]);

        var result = await SuggestAsync();

        // An plays guitar for the whole service; the song's second guitar must be someone else.
        Assert.Equal(_an, Assert.Single(roster.Assignments, x => x.SkillId == _guitar.Id && x.SongListItemId == null).MemberId);
        Assert.Equal(_binh, Assert.Single(roster.Assignments, x => x.SkillId == _guitar.Id && x.SongListItemId == _entrance.Id).MemberId);
        Assert.DoesNotContain(result.Value!.Shortages, x => x.SkillId == _guitar.Id);
    }

    [Fact]
    public async Task Suggest_SongNeedsMoreThanWholeService_NobodyElse_ReportsTheRestShort_Async()
    {
        ExistingRoster();
        EventNeedsGuitar();
        _entrance.PersonnelRequirements.Single(x => x.SkillId == _guitar.Id).RequiredCount = 2;

        var result = await SuggestAsync();

        // Only An plays guitar and already holds the whole service, so the song is one short, not two.
        var shortage = Assert.Single(result.Value!.Shortages, x => x.SkillId == _guitar.Id);
        Assert.Equal((_entrance.Id, 1, 0), (shortage.SongListItemId!.Value, shortage.RequiredCount, shortage.AssignedCount));
    }

    [Fact]
    public async Task ReplaceAssignment_WholeServiceLineAfterEarlyFinalization_IsAllowed_Async()
    {
        EventNeedsGuitar();
        var guitarist = GuitarWholeServiceLine(_an);
        var roster = FinalizableRoster(RosterStatus.Suggested, guitarist);
        roster.Assignments.Add(guitarist);
        _repository.GetRosterByAssignmentAsync(guitarist.Id, _ct).Returns(roster);
        _repository.GetAssignmentsAsync(roster.Id, _ct)
            .Returns(_ => roster.Assignments.Where(x => x.Status == RosterAssignmentStatus.Active).ToList());
        await _sut.FinalizeWholeServiceAsync(roster.Id, _ct);
        MemberIs(_binh);

        var result = await _sut.ReplaceAssignmentAsync(guitarist.Id, new ReplaceRosterAssignmentRequest { MemberId = _binh }, _ct);

        Assert.True(result.IsSuccess);
        var replacement = roster.Assignments.Single(x => x.Status == RosterAssignmentStatus.Active);
        Assert.Equal((_binh, (Guid?)null, (DateTime?)null), (replacement.MemberId, replacement.SongListItemId, replacement.NotifiedAt));
    }

    [Fact]
    public async Task AddAssignment_SameSkillWholeServiceAndSong_ReturnsDuplicateBothWays_Async()
    {
        EventNeedsGuitar();
        var wholeService = new RosterAssignment { MemberId = _an, SkillId = _guitar.Id, Source = AssignmentSource.Manual };
        var songLine = new RosterAssignment { MemberId = _binh, SkillId = _guitar.Id, SongListItemId = _entrance.Id, Source = AssignmentSource.Manual };
        RosterWith(RosterStatus.Suggested, wholeService, songLine);
        MemberIs(_an);
        MemberIs(_binh);

        var anOnSong = await _sut.AddAssignmentAsync(new CreateRosterAssignmentRequest
        {
            EventId = _event.Id, SongListItemId = _entrance.Id, SkillId = _guitar.Id, MemberId = _an,
        }, _ct);
        var binhOnWholeService = await _sut.AddAssignmentAsync(new CreateRosterAssignmentRequest
        {
            EventId = _event.Id, SkillId = _guitar.Id, MemberId = _binh,
        }, _ct);

        Assert.Equal(
            [ErrorCodes.RosterAssignmentDuplicate, ErrorCodes.RosterAssignmentDuplicate],
            new[] { anOnSong.Code, binhOnWholeService.Code });
    }
}

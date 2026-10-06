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
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RosterAssignmentProfile>(), NullLoggerFactory.Instance).CreateMapper();
        _currentUser.UserId.Returns(_directorId);
        _sut = new RosterService(_repository, _generator, _currentUser, mapper);

        _event = new LiturgicalEvent
        {
            Id = Guid.NewGuid(),
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
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

        AiFails();
    }

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

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
    }

    [Fact]
    public async Task Suggest_CancelledEvent_ReturnsEventCancelled_Async()
    {
        _event.Status = EventStatus.Cancelled;

        Assert.Equal(ErrorCodes.EventCancelled, (await SuggestAsync()).Code);
    }

    [Fact]
    public async Task Suggest_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        _event.EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        Assert.Equal(ErrorCodes.EventAlreadyPassed, (await SuggestAsync()).Code);
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

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
    }

    [Fact]
    public async Task GetShortages_NoApprovedSongList_ReturnsSongListNotApproved_Async()
    {
        _repository.GetApprovedSongListAsync(_event.Id, _ct).Returns((SongList?)null);

        Assert.Equal(ErrorCodes.RosterSongListNotApproved, (await _sut.GetShortagesAsync(_event.Id, _ct)).Code);
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

        Assert.Equal(ErrorCodes.AssignmentMemberSkillNotApproved, (await AddAsync(_an)).Code);
    }

    [Fact]
    public async Task AddAssignment_ParticipationNotConfirmed_ReturnsNotConfirmed_Async()
    {
        MemberIs(_an, confirmed: false);

        Assert.Equal(ErrorCodes.AssignmentMemberNotConfirmed, (await AddAsync(_an)).Code);
    }

    [Fact]
    public async Task AddAssignment_MemberAlreadyHoldsSlot_ReturnsDuplicate_Async()
    {
        RosterWith(RosterStatus.Suggested, SopranoLine(_an));
        MemberIs(_an);

        Assert.Equal(ErrorCodes.AssignmentDuplicate, (await AddAsync(_an)).Code);
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

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
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
    public async Task ReplaceAssignment_AlreadyReplacedLine_ReturnsAssignmentNotFound_Async()
    {
        var old = SopranoLine(_binh);
        old.Status = RosterAssignmentStatus.Replaced;
        RosterWith(RosterStatus.Suggested, old);
        MemberIs(_an);

        var result = await _sut.ReplaceAssignmentAsync(old.Id, new ReplaceRosterAssignmentRequest { MemberId = _an }, _ct);

        Assert.Equal(ErrorCodes.AssignmentNotFound, result.Code);
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
        Assert.Equal(ErrorCodes.AssignmentNotFound, (await _sut.RemoveAssignmentAsync(Guid.NewGuid(), _ct)).Code);
    }

    [Fact]
    public async Task RemoveAssignment_PastEvent_ReturnsEventAlreadyPassed_Async()
    {
        var line = SopranoLine(_binh);
        RosterWith(RosterStatus.Suggested, line);
        _event.EventDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        Assert.Equal(ErrorCodes.EventAlreadyPassed, (await _sut.RemoveAssignmentAsync(line.Id, _ct)).Code);
    }
}

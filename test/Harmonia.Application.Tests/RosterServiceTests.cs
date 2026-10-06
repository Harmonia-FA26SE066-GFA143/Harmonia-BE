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
}

using AutoMapper;
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

public class SongPersonnelRequirementServiceTests
{
    private readonly ISongListItemRepository _repository = Substitute.For<ISongListItemRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly SongPersonnelRequirementService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly Skill _soprano = new() { Id = Guid.NewGuid(), Name = "Soprano", CategoryId = SkillCategoryIds.Vocal, IsActive = true };
    private readonly Skill _guitar = new() { Id = Guid.NewGuid(), Name = "Guitar", CategoryId = SkillCategoryIds.Instrument, IsActive = true };

    public SongPersonnelRequirementServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<SongPersonnelRequirementProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _currentUser.RoleName.Returns(RoleNames.ChoirDirector);
        _sut = new SongPersonnelRequirementService(_repository, _currentUser, mapper);
    }

    private SongListItem StubItem(
        SongListStatus listStatus = SongListStatus.Draft, EventStatus eventStatus = EventStatus.Draft,
        RosterStatus? rosterStatus = null, bool isLatest = true)
    {
        var liturgicalEvent = new LiturgicalEvent { Id = Guid.NewGuid(), Status = eventStatus };
        if (rosterStatus is { } status) liturgicalEvent.ServiceRoster = new ServiceRoster { Status = status };

        var songList = new SongList { Id = Guid.NewGuid(), EventId = liturgicalEvent.Id, Version = 1, Status = listStatus, LiturgicalEvent = liturgicalEvent };
        var item = new SongListItem { Id = Guid.NewGuid(), SongListId = songList.Id, SongList = songList };

        _repository.GetWithPersonnelRequirementsAsync(item.Id, _ct).Returns(item);
        _repository.IsLatestVersionAsync(liturgicalEvent.Id, 1, _ct).Returns(isLatest);
        _repository.GetSkillsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(new Dictionary<Guid, Skill> { [_soprano.Id] = _soprano, [_guitar.Id] = _guitar });
        return item;
    }

    private static UpdateSongPersonnelRequirementsRequest Request(params (Guid SkillId, int Count)[] requirements) =>
        new() { Requirements = requirements.Select(x => new UpdateSongPersonnelRequirementRequest { SkillId = x.SkillId, RequiredCount = x.Count }).ToList() };

    [Fact]
    public async Task Update_Valid_AddsUpdatesAndRemovesRequirements_Async()
    {
        var item = StubItem();
        var alto = new Skill { Id = Guid.NewGuid(), Name = "Alto", IsActive = true };
        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 2 });
        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = alto.Id, Skill = alto, RequiredCount = 3 });

        var result = await _sut.UpdateAsync(item.Id, Request((_soprano.Id, 4), (_guitar.Id, 1)), _ct);

        Assert.True(result.IsSuccess);
        Assert.Collection(result.Value!,
            x => { Assert.Equal("Guitar", x.SkillName); Assert.Equal(1, x.RequiredCount); Assert.Equal(SkillCategoryIds.Instrument, x.SkillCategoryId); },
            x => { Assert.Equal("Soprano", x.SkillName); Assert.Equal(4, x.RequiredCount); });
        Assert.DoesNotContain(item.PersonnelRequirements, x => x.SkillId == alto.Id);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_EmptyList_RemovesEverything_Async()
    {
        var item = StubItem();
        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 2 });

        var result = await _sut.UpdateAsync(item.Id, Request(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(item.PersonnelRequirements);
    }

    [Fact]
    public async Task Update_MissingItem_ReturnsSongListItemNotFound_Async()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), Request((_soprano.Id, 1)), _ct);

        Assert.Equal(ErrorCodes.SongListItemNotFound, result.Code);
    }

    [Fact]
    public async Task Update_OlderVersion_ReturnsNotLatestVersion_Async()
    {
        var item = StubItem(isLatest: false);

        var result = await _sut.UpdateAsync(item.Id, Request((_soprano.Id, 1)), _ct);

        Assert.Equal(ErrorCodes.SongListNotLatestVersion, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_RosterFinalized_ReturnsRosterAlreadyFinalized_Async()
    {
        var item = StubItem(rosterStatus: RosterStatus.Finalized);

        var result = await _sut.UpdateAsync(item.Id, Request((_soprano.Id, 1)), _ct);

        Assert.Equal(ErrorCodes.RosterAlreadyFinalized, result.Code);
    }

    [Fact]
    public async Task Update_UnknownSkill_ReturnsSkillNotFoundAndLeavesItemUntouched_Async()
    {
        var item = StubItem();
        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 2 });

        var result = await _sut.UpdateAsync(item.Id, Request((Guid.NewGuid(), 1)), _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
        Assert.Single(item.PersonnelRequirements);
    }

    [Fact]
    public async Task Update_InactiveSkill_OnlyBlocksNewlyAddedOnes_Async()
    {
        _guitar.IsActive = false;
        var item = StubItem();

        var added = await _sut.UpdateAsync(item.Id, Request((_guitar.Id, 1)), _ct);
        Assert.Equal(ErrorCodes.SkillInactive, added.Code);

        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _guitar.Id, Skill = _guitar, RequiredCount = 1 });
        var kept = await _sut.UpdateAsync(item.Id, Request((_guitar.Id, 2)), _ct);
        Assert.True(kept.IsSuccess);
    }

    [Fact]
    public async Task Get_ChoirMemberOnUnapprovedList_ReturnsSongListItemNotFound_Async()
    {
        _currentUser.RoleName.Returns(RoleNames.ChoirMember);
        var item = StubItem(listStatus: SongListStatus.Submitted, eventStatus: EventStatus.Published);

        var result = await _sut.GetAsync(item.Id, _ct);

        Assert.Equal(ErrorCodes.SongListItemNotFound, result.Code);
    }

    [Fact]
    public async Task Get_ChoirMemberOnApprovedListOfPublishedEvent_ReturnsRequirements_Async()
    {
        _currentUser.RoleName.Returns(RoleNames.ChoirMember);
        var item = StubItem(listStatus: SongListStatus.Approved, eventStatus: EventStatus.Published);
        item.PersonnelRequirements.Add(new SongPersonnelRequirement { SkillId = _soprano.Id, Skill = _soprano, RequiredCount = 2 });

        var result = await _sut.GetAsync(item.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
    }
}

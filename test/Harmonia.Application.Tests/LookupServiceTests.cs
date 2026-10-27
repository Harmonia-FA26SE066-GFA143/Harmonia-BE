using System.Linq.Expressions;
using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Application.Validators;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class LookupServiceTests
{
    private readonly IGenericRepository<MassType> _massTypes = Substitute.For<IGenericRepository<MassType>>();
    private readonly IGenericRepository<SkillCategory> _skillCategories = Substitute.For<IGenericRepository<SkillCategory>>();
    private readonly IGenericRepository<Skill> _skills = Substitute.For<IGenericRepository<Skill>>();
    private readonly IGenericRepository<LiturgicalSeason> _seasons = Substitute.For<IGenericRepository<LiturgicalSeason>>();
    private readonly IGenericRepository<SongTheme> _songThemes = Substitute.For<IGenericRepository<SongTheme>>();
    private readonly IGenericRepository<LiturgicalSlot> _slots = Substitute.For<IGenericRepository<LiturgicalSlot>>();
    private readonly IGenericRepository<WorshipLocation> _locations = Substitute.For<IGenericRepository<WorshipLocation>>();
    private readonly IGenericRepository<SongVocalRequirement> _vocalRequirements = Substitute.For<IGenericRepository<SongVocalRequirement>>();
    private readonly IGenericRepository<SongInstrumentRequirement> _instrumentRequirements = Substitute.For<IGenericRepository<SongInstrumentRequirement>>();
    private readonly IGenericRepository<LiturgicalSlotRequirement> _slotRequirements = Substitute.For<IGenericRepository<LiturgicalSlotRequirement>>();
    private readonly LookupService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LookupServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MassTypeProfile>();
            cfg.AddProfile<SkillProfile>();
            cfg.AddProfile<SkillCategoryProfile>();
            cfg.AddProfile<LiturgicalSeasonProfile>();
            cfg.AddProfile<SongThemeProfile>();
            cfg.AddProfile<LiturgicalSlotProfile>();
            cfg.AddProfile<WorshipLocationProfile>();
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new LookupService(
            _massTypes,
            Substitute.For<IGenericRepository<CeremonyType>>(),
            Substitute.For<IGenericRepository<EventCategory>>(),
            _songThemes,
            _skillCategories,
            _seasons,
            _slots,
            _locations,
            _skills,
            _vocalRequirements,
            _instrumentRequirements,
            _slotRequirements,
            mapper);

        Rows(_vocalRequirements);
        Rows(_instrumentRequirements);
    }

    /// <summary>Runs the service's conflict predicate against the given rows, like the database would.</summary>
    private static void Rows<T>(IGenericRepository<T> repository, params T[] rows) where T : BaseEntity =>
        repository.ListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => rows.Where(call.Arg<Expression<Func<T, bool>>>().Compile()).ToList());

    [Fact]
    public async Task SaveMassType_New_AddsTrimmedActiveRow_Async()
    {
        Rows(_massTypes);

        var result = await _sut.SaveMassTypeAsync(null, new SaveCatalogItemRequest { Name = "  Sunday Mass " }, _ct);

        Assert.Equal("Sunday Mass", result.Value!.Name);
        Assert.True(result.Value.IsActive);
        await _massTypes.Received(1).AddAsync(Arg.Is<MassType>(x => x.Name == "Sunday Mass" && x.Id != Guid.Empty), _ct);
        await _massTypes.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task SaveMassType_NameTakenByAnotherRow_ReturnsDuplicate_Async()
    {
        Rows(_massTypes, new MassType { Id = Guid.NewGuid(), Name = "Sunday Mass" });

        var result = await _sut.SaveMassTypeAsync(null, new SaveCatalogItemRequest { Name = "Sunday Mass" }, _ct);

        Assert.Equal(ErrorCodes.LookupNameDuplicate, result.Code);
        await _massTypes.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task SaveMassType_UpdateKeepingOwnName_DeactivatesRow_Async()
    {
        var row = new MassType { Id = Guid.NewGuid(), Name = "Sunday Mass" };
        Rows(_massTypes, row);
        _massTypes.GetByIdAsync(row.Id, _ct).Returns(row);

        var result = await _sut.SaveMassTypeAsync(row.Id, new SaveCatalogItemRequest { Name = "Sunday Mass", IsActive = false }, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(row.IsActive);
        await _massTypes.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task SaveMassType_UpdateMissingRow_ReturnsNotFound_Async()
    {
        var result = await _sut.SaveMassTypeAsync(Guid.NewGuid(), new SaveCatalogItemRequest { Name = "X" }, _ct);

        Assert.Equal(ErrorCodes.LookupNotFound, result.Code);
    }

    [Fact]
    public async Task SaveSkillCategory_SwitchingOffASeededCategory_ReturnsLookupInUse_ButRenamingWorks_Async()
    {
        var vocal = new SkillCategory { Id = SkillCategoryIds.Vocal, Name = "Vocal" };
        _skillCategories.GetByIdAsync(vocal.Id, _ct).Returns(vocal);
        Rows(_skillCategories, vocal);

        var off = await _sut.SaveSkillCategoryAsync(vocal.Id, new SaveCatalogItemRequest { Name = "Vocal", IsActive = false }, _ct);
        var renamed = await _sut.SaveSkillCategoryAsync(vocal.Id, new SaveCatalogItemRequest { Name = "Voices" }, _ct);

        Assert.Equal(ErrorCodes.LookupInUse, off.Code);
        Assert.True(vocal.IsActive);
        Assert.Equal("Voices", renamed.Value!.Name);
    }

    [Fact]
    public async Task SaveSkillCategory_SwitchingOffAnAdminMadeCategory_IsAllowed_Async()
    {
        var custom = new SkillCategory { Id = Guid.NewGuid(), Name = "Dance" };
        _skillCategories.GetByIdAsync(custom.Id, _ct).Returns(custom);
        Rows(_skillCategories, custom);

        var result = await _sut.SaveSkillCategoryAsync(custom.Id, new SaveCatalogItemRequest { Name = "Dance", IsActive = false }, _ct);

        Assert.False(result.Value!.IsActive);
    }

    /// <summary>A skill in <paramref name="fromCategoryId"/>, plus the category the test moves it to.</summary>
    private Skill ExistingSkill(Guid fromCategoryId, Guid toCategoryId, bool isTargetActive = true)
    {
        var skill = new Skill { Id = Guid.NewGuid(), CategoryId = fromCategoryId, Name = "Organ" };
        _skills.GetByIdAsync(skill.Id, _ct).Returns(skill);
        _skillCategories.GetByIdAsync(toCategoryId, _ct).Returns(new SkillCategory { Id = toCategoryId, IsActive = isTargetActive });
        Rows(_skills, skill);
        return skill;
    }

    [Fact]
    public async Task SaveSkill_IntoSwitchedOffCategory_ReturnsLookupInactive_Async()
    {
        var off = Guid.NewGuid();
        var moved = ExistingSkill(SkillCategoryIds.Vocal, off, isTargetActive: false);

        var created = await _sut.SaveSkillAsync(null, new SaveSkillRequest { CategoryId = off, Name = "New" }, _ct);
        var movedIn = await _sut.SaveSkillAsync(moved.Id, new SaveSkillRequest { CategoryId = off, Name = "Organ" }, _ct);

        Assert.Equal(ErrorCodes.LookupInactive, created.Code);
        Assert.Equal(ErrorCodes.LookupInactive, movedIn.Code);
    }

    [Fact]
    public async Task SaveSkill_EditedInsideItsSwitchedOffCategory_IsAllowed_Async()
    {
        var off = Guid.NewGuid();
        var skill = ExistingSkill(off, off, isTargetActive: false);

        var result = await _sut.SaveSkillAsync(skill.Id, new SaveSkillRequest { CategoryId = off, Name = "Pipe organ" }, _ct);

        Assert.Equal("Pipe organ", result.Value!.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveSkill_MovedAcrossTheInstrumentBoundaryWhileASongRequiresIt_ReturnsSkillInUse_Async(bool leavingInstrument)
    {
        var (from, to) = leavingInstrument
            ? (SkillCategoryIds.Instrument, SkillCategoryIds.Vocal)
            : (SkillCategoryIds.Vocal, SkillCategoryIds.Instrument);
        var skill = ExistingSkill(from, to);
        if (leavingInstrument) Rows(_instrumentRequirements, new SongInstrumentRequirement { SkillId = skill.Id });
        else Rows(_vocalRequirements, new SongVocalRequirement { SkillId = skill.Id });

        var result = await _sut.SaveSkillAsync(skill.Id, new SaveSkillRequest { CategoryId = to, Name = "Organ" }, _ct);

        Assert.Equal(ErrorCodes.SkillInUse, result.Code);
        Assert.Equal(from, skill.CategoryId);
    }

    [Fact]
    public async Task SaveSkill_MoveThatNoSongDependsOn_IsAllowed_Async()
    {
        // An unused skill crossing the boundary.
        var unused = ExistingSkill(SkillCategoryIds.Vocal, SkillCategoryIds.Instrument);

        var crossed = await _sut.SaveSkillAsync(
            unused.Id, new SaveSkillRequest { CategoryId = SkillCategoryIds.Instrument, Name = "Organ" }, _ct);

        Assert.Equal(SkillCategoryIds.Instrument, crossed.Value!.CategoryId);
    }

    [Fact]
    public async Task SaveSkill_RequiredSkillMovedBetweenNonInstrumentCategories_IsAllowed_Async()
    {
        var required = ExistingSkill(SkillCategoryIds.Vocal, SkillCategoryIds.Solo);
        Rows(_vocalRequirements, new SongVocalRequirement { SkillId = required.Id });

        var sideways = await _sut.SaveSkillAsync(
            required.Id, new SaveSkillRequest { CategoryId = SkillCategoryIds.Solo, Name = "Organ" }, _ct);

        Assert.Equal(SkillCategoryIds.Solo, sideways.Value!.CategoryId);
    }

    [Fact]
    public async Task SaveSkill_UpdateMissingSkill_ReturnsSkillNotFound_Async()
    {
        var category = new SkillCategory { Id = Guid.NewGuid() };
        _skillCategories.GetByIdAsync(category.Id, _ct).Returns(category);

        var result = await _sut.SaveSkillAsync(Guid.NewGuid(), new SaveSkillRequest { CategoryId = category.Id, Name = "Organ" }, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
    }

    [Fact]
    public async Task SaveSkill_MissingCategory_ReturnsCategoryNotFound_Async()
    {
        var result = await _sut.SaveSkillAsync(null, new SaveSkillRequest { CategoryId = Guid.NewGuid(), Name = "Violin" }, _ct);

        Assert.Equal(ErrorCodes.SkillCategoryNotFound, result.Code);
    }

    [Fact]
    public async Task SaveSkill_SameNameInOtherCategory_IsAllowed_Async()
    {
        var category = new SkillCategory { Id = Guid.NewGuid() };
        _skillCategories.GetByIdAsync(category.Id, _ct).Returns(category);
        Rows(_skills, new Skill { Id = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Solo" });

        var result = await _sut.SaveSkillAsync(null, new SaveSkillRequest { CategoryId = category.Id, Name = "Solo" }, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SaveSkill_SameNameInSameCategory_ReturnsSkillNameDuplicate_Async()
    {
        var category = new SkillCategory { Id = Guid.NewGuid() };
        _skillCategories.GetByIdAsync(category.Id, _ct).Returns(category);
        Rows(_skills, new Skill { Id = Guid.NewGuid(), CategoryId = category.Id, Name = "Solo" });

        var result = await _sut.SaveSkillAsync(null, new SaveSkillRequest { CategoryId = category.Id, Name = "Solo" }, _ct);

        Assert.Equal(ErrorCodes.SkillNameDuplicate, result.Code);
    }

    // ---- Song themes, liturgical slots, worship locations ----

    [Fact]
    public async Task SaveSongThemeSlotAndLocation_New_StoreTheirOwnFields_Async()
    {
        Rows(_songThemes);
        Rows(_slots);
        Rows(_locations);

        var theme = await _sut.SaveSongThemeAsync(null, new SaveSongThemeRequest { Name = " Marian ", Description = "For Mary" }, _ct);
        var slot = await _sut.SaveLiturgicalSlotAsync(null, new SaveLiturgicalSlotRequest { Name = " Entrance ", DefaultOrder = 1 }, _ct);
        var location = await _sut.SaveWorshipLocationAsync(
            null, new SaveWorshipLocationRequest { Name = " Chapel ", Address = "12 Main St", IsActive = false }, _ct);

        Assert.Equal(("Marian", "For Mary", true), (theme.Value!.Name, theme.Value.Description, theme.Value.IsActive));
        Assert.Equal(("Entrance", 1, true), (slot.Value!.Name, slot.Value.DefaultOrder, slot.Value.IsActive));
        Assert.Equal(("Chapel", "12 Main St", false), (location.Value!.Name, location.Value.Address, location.Value.IsActive));
        await _locations.Received(1).AddAsync(Arg.Is<WorshipLocation>(x => x.Name == "Chapel"), _ct);
    }

    [Fact]
    public async Task SaveSongThemeSlotAndLocation_NameTaken_ReturnDuplicate_AndMissingRowReturnsNotFound_Async()
    {
        Rows(_songThemes, new SongTheme { Id = Guid.NewGuid(), Name = "Marian" });
        Rows(_slots, new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Entrance" });
        Rows(_locations, new WorshipLocation { Id = Guid.NewGuid(), Name = "Chapel" });

        var theme = await _sut.SaveSongThemeAsync(null, new SaveSongThemeRequest { Name = "Marian" }, _ct);
        var slot = await _sut.SaveLiturgicalSlotAsync(null, new SaveLiturgicalSlotRequest { Name = "Entrance" }, _ct);
        var location = await _sut.SaveWorshipLocationAsync(null, new SaveWorshipLocationRequest { Name = "Chapel" }, _ct);
        var missing = await _sut.SaveWorshipLocationAsync(Guid.NewGuid(), new SaveWorshipLocationRequest { Name = "Other" }, _ct);

        Assert.Equal(
            [ErrorCodes.LookupNameDuplicate, ErrorCodes.LookupNameDuplicate, ErrorCodes.LookupNameDuplicate, ErrorCodes.LookupNotFound],
            new[] { theme.Code, slot.Code, location.Code, missing.Code });
    }

    [Fact]
    public async Task SaveSongTheme_Parent_StoredWhenValid_RejectedWhenMissingOrDescendant_Async()
    {
        var mary = new SongTheme { Id = Guid.NewGuid(), Name = "Mary" };
        var rosary = new SongTheme { Id = Guid.NewGuid(), Name = "Rosary", ParentId = mary.Id };
        Rows(_songThemes, mary, rosary);

        var child = await _sut.SaveSongThemeAsync(null, new SaveSongThemeRequest { Name = "Assumption", ParentId = mary.Id }, _ct);
        var missing = await _sut.SaveSongThemeAsync(null, new SaveSongThemeRequest { Name = "Other", ParentId = Guid.NewGuid() }, _ct);
        var self = await _sut.SaveSongThemeAsync(mary.Id, new SaveSongThemeRequest { Name = "Mary", ParentId = mary.Id }, _ct);
        var cycle = await _sut.SaveSongThemeAsync(mary.Id, new SaveSongThemeRequest { Name = "Mary", ParentId = rosary.Id }, _ct);

        Assert.Equal(mary.Id, child.Value!.ParentId);
        Assert.Equal(
            [ErrorCodes.SongThemeParentInvalid, ErrorCodes.SongThemeParentInvalid, ErrorCodes.SongThemeParentInvalid],
            new[] { missing.Code, self.Code, cycle.Code });
        Assert.Null(mary.ParentId);
    }

    [Fact]
    public async Task UpdateLiturgicalSlotRequirements_ReplacesTheSet_AndRejectsUnknownSkill_Async()
    {
        var slot = new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Psalm" };
        _slots.GetByIdAsync(slot.Id, _ct).Returns(slot);
        var psalmist = new Skill { Id = Guid.NewGuid(), Name = "Psalmist", IsActive = true };
        var solo = new Skill { Id = Guid.NewGuid(), Name = "Solo", IsActive = true };
        var organ = new Skill { Id = Guid.NewGuid(), Name = "Organ", IsActive = true };
        Rows(_skills, psalmist, solo, organ);
        var kept = new LiturgicalSlotRequirement { Id = Guid.NewGuid(), SlotId = slot.Id, SkillId = psalmist.Id, RequiredCount = 1 };
        var dropped = new LiturgicalSlotRequirement { Id = Guid.NewGuid(), SlotId = slot.Id, SkillId = organ.Id, RequiredCount = 1 };
        Rows(_slotRequirements, kept, dropped);
        _slotRequirements.GetByIdAsync(kept.Id, _ct).Returns(kept);

        var unknown = await _sut.UpdateLiturgicalSlotRequirementsAsync(slot.Id, new UpdateLiturgicalSlotRequirementsRequest
        {
            Requirements = [new() { SkillId = Guid.NewGuid(), RequiredCount = 1 }],
        }, _ct);
        var result = await _sut.UpdateLiturgicalSlotRequirementsAsync(slot.Id, new UpdateLiturgicalSlotRequirementsRequest
        {
            Requirements = [new() { SkillId = psalmist.Id, RequiredCount = 2 }, new() { SkillId = solo.Id, RequiredCount = 1 }],
        }, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, unknown.Code);
        Assert.Equal([("Psalmist", 2), ("Solo", 1)], result.Value!.Select(x => (x.SkillName, x.RequiredCount)));
        _slotRequirements.Received(1).Remove(dropped);
        await _slotRequirements.Received(1).AddAsync(Arg.Is<LiturgicalSlotRequirement>(x => x.SkillId == solo.Id && x.SlotId == slot.Id), _ct);
    }

    // ---- Catalog rows an event points at ----

    private readonly DateOnly _eventDate = new(2026, 12, 25);

    private WorshipLocation Location(bool isActive = true)
    {
        var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Main church", IsActive = isActive };
        _locations.GetByIdAsync(location.Id, _ct).Returns(location);
        return location;
    }

    private MassType MassTypeRow(bool isActive = true)
    {
        var massType = new MassType { Id = Guid.NewGuid(), Name = "Sunday Mass", IsActive = isActive };
        _massTypes.GetByIdAsync(massType.Id, _ct).Returns(massType);
        return massType;
    }

    private LiturgicalSeason Season(DateOnly start, DateOnly end)
    {
        var season = new LiturgicalSeason { Id = Guid.NewGuid(), Name = "Christmas", StartDate = start, EndDate = end };
        _seasons.GetByIdAsync(season.Id, _ct).Returns(season);
        return season;
    }

    [Fact]
    public async Task CheckEventCatalogs_ActiveRowsAndDateInsideTheSeason_Succeeds_Async()
    {
        var season = Season(_eventDate, _eventDate.AddDays(10));

        var result = await _sut.CheckEventCatalogsAsync(
            _eventDate, season.Id, MassTypeRow().Id, null, null, Location().Id, null, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckEventCatalogs_UnknownId_ReturnsLookupNotFound_Async()
    {
        var unknownMassType = await _sut.CheckEventCatalogsAsync(_eventDate, null, Guid.NewGuid(), null, null, Location().Id, null, _ct);
        var unknownLocation = await _sut.CheckEventCatalogsAsync(_eventDate, null, MassTypeRow().Id, null, null, Guid.NewGuid(), null, _ct);
        var unknownSeason = await _sut.CheckEventCatalogsAsync(_eventDate, Guid.NewGuid(), null, null, null, Location().Id, null, _ct);

        Assert.All(
            new[] { unknownMassType, unknownLocation, unknownSeason }, r => Assert.Equal(ErrorCodes.LookupNotFound, r.Code));
    }

    [Fact]
    public async Task CheckEventCatalogs_SwitchedOffRow_ReturnsLookupInactive_UnlessTheEventAlreadyUsesIt_Async()
    {
        var retired = MassTypeRow(isActive: false);
        var closed = Location(isActive: false);
        var current = new LiturgicalEvent { MassTypeId = retired.Id, LocationId = closed.Id };

        var onCreate = await _sut.CheckEventCatalogsAsync(_eventDate, null, retired.Id, null, null, Location().Id, null, _ct);
        var newLocation = await _sut.CheckEventCatalogsAsync(_eventDate, null, MassTypeRow().Id, null, null, closed.Id, null, _ct);
        var unchanged = await _sut.CheckEventCatalogsAsync(_eventDate, null, retired.Id, null, null, closed.Id, current, _ct);

        Assert.Equal(ErrorCodes.LookupInactive, onCreate.Code);
        Assert.Equal(ErrorCodes.LookupInactive, newLocation.Code);
        Assert.True(unchanged.IsSuccess);
    }

    [Fact]
    public async Task CheckEventCatalogs_DayBeforeTheSeasonStarts_Succeeds_ForAVigilMass_Async()
    {
        var season = Season(_eventDate, _eventDate.AddDays(10));

        var result = await _sut.CheckEventCatalogsAsync(
            _eventDate.AddDays(-1), season.Id, MassTypeRow().Id, null, null, Location().Id, null, _ct);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(11)]
    public async Task CheckEventCatalogs_DateOutsideTheSeason_ReturnsEventDateOutsideSeason_EvenWhenUnchanged_Async(int offsetDays)
    {
        var season = Season(_eventDate, _eventDate.AddDays(10));
        var location = Location();
        var current = new LiturgicalEvent { LiturgicalSeasonId = season.Id, LocationId = location.Id };

        var result = await _sut.CheckEventCatalogsAsync(
            _eventDate.AddDays(offsetDays), season.Id, null, null, null, location.Id, current, _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason, result.Code);
    }

    [Fact]
    public async Task SaveSeason_OverlapsActiveSeason_ReturnsOverlap_Async()
    {
        Rows(_seasons, new LiturgicalSeason
        {
            Id = Guid.NewGuid(), Name = "Advent", StartDate = new DateOnly(2026, 11, 29), EndDate = new DateOnly(2026, 12, 24),
        });

        var result = await _sut.SaveLiturgicalSeasonAsync(null, new SaveLiturgicalSeasonRequest
        {
            Name = "Christmas", StartDate = new DateOnly(2026, 12, 24), EndDate = new DateOnly(2027, 1, 10),
        }, _ct);

        Assert.Equal(ErrorCodes.LiturgicalSeasonDateOverlap, result.Code);
    }

    [Fact]
    public async Task SaveSeason_OverlapsOnlyInactiveSeason_IsAllowed_Async()
    {
        Rows(_seasons, new LiturgicalSeason
        {
            Id = Guid.NewGuid(), Name = "Old", StartDate = new DateOnly(2026, 11, 29), EndDate = new DateOnly(2026, 12, 24), IsActive = false,
        });

        var result = await _sut.SaveLiturgicalSeasonAsync(null, new SaveLiturgicalSeasonRequest
        {
            Name = "Advent", StartDate = new DateOnly(2026, 11, 29), EndDate = new DateOnly(2026, 12, 24),
        }, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SeasonValidator_EndNotAfterStart_ReturnsSeasonDateInvalid_Async()
    {
        var result = await new SaveLiturgicalSeasonRequestValidator().ValidateAsync(new SaveLiturgicalSeasonRequest
        {
            Name = "Advent", StartDate = new DateOnly(2026, 12, 1), EndDate = new DateOnly(2026, 12, 1),
        }, _ct);

        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.LiturgicalSeasonDateInvalid);
    }

    [Fact]
    public async Task SaveSongTheme_NoActiveChildUnderASwitchedOffParent_Async()
    {
        var mary = new SongTheme { Id = Guid.NewGuid(), Name = "Mary", IsActive = true };
        var rosary = new SongTheme { Id = Guid.NewGuid(), Name = "Rosary", ParentId = mary.Id, IsActive = true };
        var closed = new SongTheme { Id = Guid.NewGuid(), Name = "Closed", IsActive = false };
        Rows(_songThemes, mary, rosary, closed);

        var switchOffParent = await _sut.SaveSongThemeAsync(mary.Id, new SaveSongThemeRequest { Name = "Mary", IsActive = false }, _ct);
        var activeUnderClosed = await _sut.SaveSongThemeAsync(null, new SaveSongThemeRequest { Name = "New", ParentId = closed.Id }, _ct);

        Assert.Equal(ErrorCodes.SongThemeHasActiveChildren, switchOffParent.Code);
        Assert.Equal(ErrorCodes.SongThemeParentInvalid, activeUnderClosed.Code);
    }
}

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
    private readonly IGenericRepository<SongVocalRequirement> _vocalRequirements = Substitute.For<IGenericRepository<SongVocalRequirement>>();
    private readonly IGenericRepository<SongInstrumentRequirement> _instrumentRequirements = Substitute.For<IGenericRepository<SongInstrumentRequirement>>();
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
        }, NullLoggerFactory.Instance).CreateMapper();

        _sut = new LookupService(
            _massTypes,
            Substitute.For<IGenericRepository<CeremonyType>>(),
            Substitute.For<IGenericRepository<EventCategory>>(),
            Substitute.For<IGenericRepository<SongTheme>>(),
            _skillCategories,
            _seasons,
            Substitute.For<IGenericRepository<LiturgicalSlot>>(),
            Substitute.For<IGenericRepository<WorshipLocation>>(),
            _skills,
            _vocalRequirements,
            _instrumentRequirements,
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

        Assert.Equal(ErrorCodes.SeasonDateOverlap, result.Code);
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

        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.SeasonDateInvalid);
    }
}

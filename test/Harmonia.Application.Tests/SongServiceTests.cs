using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class SongServiceTests
{
    private readonly ISongRepository _repository = Substitute.For<ISongRepository>();
    private readonly SongService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public SongServiceTests()
    {
        // Real profile, so the trim / blank-to-null transformer is covered too.
        var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<SongProfile>();
                cfg.AddProfile<SongVocalRequirementProfile>();
                cfg.AddProfile<SongInstrumentRequirementProfile>();
            }, NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new SongService(_repository, mapper);
    }

    private Song StubSong(bool isActive = true)
    {
        var song = new Song { Id = Guid.NewGuid(), Title = "Kinh Hoa Binh", IsActive = isActive };
        _repository.GetByIdAsync(song.Id, _ct).Returns(song);
        return song;
    }

    [Fact]
    public async Task Create_Valid_TrimsFieldsAndSaves_Async()
    {
        var result = await _sut.CreateAsync(
            new CreateSongRequest { Title = "  Kinh Hoa Binh  ", Composer = "   " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kinh Hoa Binh", result.Value!.Title);
        Assert.Null(result.Value.Composer);
        await _repository.Received(1).AddAsync(
            Arg.Is<Song>(s => s.Id != Guid.Empty && s.IsActive && s.Title == "Kinh Hoa Binh" && s.Composer == null), _ct);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Create_DuplicateTitleAndComposer_ReturnsSongTitleDuplicate_Async()
    {
        _repository.ExistsByTitleAsync("Kinh Hoa Binh", "Kim Long", null, _ct).Returns(true);

        var result = await _sut.CreateAsync(
            new CreateSongRequest { Title = "Kinh Hoa Binh", Composer = "Kim Long" }, _ct);

        Assert.Equal(ErrorCodes.SongTitleDuplicate, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsSongNotFound_Async()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task GetById_Deleted_ReturnsSongNotFound_Async()
    {
        var song = StubSong(isActive: false);

        var result = await _sut.GetByIdAsync(song.Id, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task Update_Valid_MapsOntoEntityAndSaves_Async()
    {
        var song = StubSong();

        var result = await _sut.UpdateAsync(
            song.Id, new UpdateSongRequest { Title = "Xin Vang Loi Chua", Tempo = " Andante " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Xin Vang Loi Chua", song.Title);
        Assert.Equal("Andante", song.Tempo);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_Deleted_ReturnsSongNotFound_Async()
    {
        var song = StubSong(isActive: false);

        var result = await _sut.UpdateAsync(song.Id, new UpdateSongRequest { Title = "X" }, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_DuplicateExcludingSelf_ReturnsSongTitleDuplicate_Async()
    {
        var song = StubSong();
        _repository.ExistsByTitleAsync("Taken", null, song.Id, _ct).Returns(true);

        var result = await _sut.UpdateAsync(song.Id, new UpdateSongRequest { Title = "Taken" }, _ct);

        Assert.Equal(ErrorCodes.SongTitleDuplicate, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Delete_Active_SetsInactiveAndSaves_Async()
    {
        var song = StubSong();

        var result = await _sut.DeleteAsync(song.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(song.IsActive);
        _repository.DidNotReceiveWithAnyArgs().Remove(default!);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_ReturnsSongNotFound_Async()
    {
        var song = StubSong(isActive: false);

        var result = await _sut.DeleteAsync(song.Id, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    // ---- Classification (FE-29) ----

    private readonly Dictionary<Guid, (string Name, bool IsActive)> _lookups = [];
    private readonly Dictionary<Guid, Skill> _skills = [];

    private Song StubClassifiedSong(bool isActive = true)
    {
        var song = new Song { Id = Guid.NewGuid(), Title = "Kinh Hoa Binh", IsActive = isActive };
        _repository.GetWithClassificationAsync(song.Id, _ct).Returns(song);
        _repository.GetClassificationTargetsAsync(Arg.Any<ClassificationTarget>(), Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(ci => _lookups.Where(x => ci.Arg<IReadOnlyCollection<Guid>>().Contains(x.Key)).ToDictionary());
        _repository.GetSkillsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _ct)
            .Returns(ci => _skills.Where(x => ci.Arg<IReadOnlyCollection<Guid>>().Contains(x.Key)).ToDictionary());
        return song;
    }

    private Guid StubLookup(string name, bool isActive = true)
    {
        var id = Guid.NewGuid();
        _lookups[id] = (name, isActive);
        return id;
    }

    private Skill StubSkill(Guid categoryId, string name, bool isActive = true)
    {
        var skill = new Skill { Id = Guid.NewGuid(), CategoryId = categoryId, Name = name, IsActive = isActive };
        _skills[skill.Id] = skill;
        return skill;
    }

    [Fact]
    public async Task GetClassification_Deleted_ReturnsSongNotFound_Async()
    {
        var song = StubClassifiedSong(isActive: false);

        var result = await _sut.GetClassificationAsync(song.Id, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task UpdateClassification_Missing_ReturnsSongNotFound_Async()
    {
        var result = await _sut.UpdateClassificationAsync(Guid.NewGuid(), new UpdateSongClassificationRequest(), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateClassification_Valid_ReplacesWholeSetAndSaves_Async()
    {
        var song = StubClassifiedSong();
        var lent = StubLookup("Lent");
        var advent = StubLookup("Advent");
        var soprano = StubSkill(SkillCategoryIds.Vocal, "Soprano");
        var alto = StubSkill(SkillCategoryIds.Vocal, "Alto");
        var guitar = StubSkill(SkillCategoryIds.Instrument, "Guitar");
        song.Classifications.Add(new SongClassification { TargetType = ClassificationTarget.LiturgicalSeason, TargetId = lent });
        song.VocalRequirements.Add(new SongVocalRequirement { SkillId = soprano.Id, Skill = soprano, IsMandatory = false });
        song.VocalRequirements.Add(new SongVocalRequirement { SkillId = alto.Id, Skill = alto });

        var result = await _sut.UpdateClassificationAsync(song.Id, new UpdateSongClassificationRequest
        {
            LiturgicalSeasonIds = [advent],
            VocalRequirements = [new() { SkillId = soprano.Id, IsMandatory = true }],
            InstrumentRequirements = [new() { SkillId = guitar.Id }],
        }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(advent, Assert.Single(song.Classifications).TargetId);
        Assert.True(Assert.Single(song.VocalRequirements).IsMandatory);
        Assert.Equal(guitar.Id, Assert.Single(song.InstrumentRequirements).SkillId);
        Assert.Equal("Advent", Assert.Single(result.Value!.LiturgicalSeasons).Name);
        Assert.Equal("Guitar", Assert.Single(result.Value.InstrumentRequirements).SkillName);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateClassification_KeepsTargetDeactivatedSince_Async()
    {
        var song = StubClassifiedSong();
        var oldTheme = StubLookup("Old theme", isActive: false);
        song.Classifications.Add(new SongClassification { TargetType = ClassificationTarget.SongTheme, TargetId = oldTheme });

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { SongThemeIds = [oldTheme] }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Old theme", Assert.Single(result.Value!.SongThemes).Name);
    }

    [Fact]
    public async Task UpdateClassification_NewInactiveTarget_ReturnsTargetInvalid_Async()
    {
        var song = StubClassifiedSong();
        var inactive = StubLookup("Retired", isActive: false);

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { MassTypeIds = [inactive] }, _ct);

        Assert.Equal(ErrorCodes.SongClassificationTargetInvalid, result.Code);
        Assert.Empty(song.Classifications);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateClassification_UnknownTarget_ReturnsTargetInvalid_Async()
    {
        var song = StubClassifiedSong();

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { CeremonyTypeIds = [Guid.NewGuid()] }, _ct);

        Assert.Equal(ErrorCodes.SongClassificationTargetInvalid, result.Code);
    }

    [Fact]
    public async Task UpdateClassification_InstrumentInVocalList_ReturnsCategoryInvalid_Async()
    {
        var song = StubClassifiedSong();
        var guitar = StubSkill(SkillCategoryIds.Instrument, "Guitar");

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { VocalRequirements = [new() { SkillId = guitar.Id }] }, _ct);

        Assert.Equal(ErrorCodes.SongSkillRequirementCategoryInvalid, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task UpdateClassification_VocalInInstrumentList_ReturnsCategoryInvalid_Async()
    {
        var song = StubClassifiedSong();
        var psalmist = StubSkill(SkillCategoryIds.Psalm, "Psalmist");

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { InstrumentRequirements = [new() { SkillId = psalmist.Id }] }, _ct);

        Assert.Equal(ErrorCodes.SongSkillRequirementCategoryInvalid, result.Code);
    }

    [Fact]
    public async Task UpdateClassification_UnknownSkill_ReturnsSkillNotFound_Async()
    {
        var song = StubClassifiedSong();

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { VocalRequirements = [new() { SkillId = Guid.NewGuid() }] }, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
    }

    [Fact]
    public async Task UpdateClassification_NewInactiveSkill_ReturnsSkillInactive_Async()
    {
        var song = StubClassifiedSong();
        var tenor = StubSkill(SkillCategoryIds.Vocal, "Tenor", isActive: false);

        var result = await _sut.UpdateClassificationAsync(
            song.Id, new UpdateSongClassificationRequest { VocalRequirements = [new() { SkillId = tenor.Id }] }, _ct);

        Assert.Equal(ErrorCodes.SkillInactive, result.Code);
    }
}

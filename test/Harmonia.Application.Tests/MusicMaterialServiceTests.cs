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
using NSubstitute.ExceptionExtensions;

namespace Harmonia.Application.Tests;

public class MusicMaterialServiceTests
{
    private readonly IMusicMaterialRepository _materials = Substitute.For<IMusicMaterialRepository>();
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly IGenericRepository<Skill> _skills = Substitute.For<IGenericRepository<Skill>>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IMaterialLearningProgressRepository _progresses = Substitute.For<IMaterialLearningProgressRepository>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly MusicMaterialService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Song _song = new() { Id = Guid.NewGuid(), Title = "Kinh Hoa Binh", IsActive = true };

    public MusicMaterialServiceTests()
    {
        var mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MusicMaterialProfile>();
                cfg.AddProfile<MaterialLearningProgressProfile>();
                cfg.AddProfile<MemberProfileProfile>();
            }, NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MusicMaterialService(_materials, _songs, _skills, _members, _progresses, _storage, mapper);

        _songs.GetByIdAsync(_song.Id, _ct).Returns(_song);
        _storage.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(call => Result<FileUploadResponse>.Success(new FileUploadResponse(
                $"harmonia/{call.ArgAt<string>(2)}/stored{Path.GetExtension(call.ArgAt<string>(1))}", "unused")));
        _storage.DeleteAsync(default!, default, default).ReturnsForAnyArgs(Result.Success());
        _storage.GetSignedUrl(Arg.Any<string>()).Returns(call => $"signed:{call.Arg<string>()}");
    }

    private UploadMusicMaterialRequest NewRequest(MaterialType type = MaterialType.SheetMusic) =>
        new() { SongId = _song.Id, Title = " Ban nhac ", MaterialType = type };

    private static FileContent NewFile(string fileName, long length = 1024) =>
        new(new MemoryStream([1, 2, 3]), fileName, length);

    [Fact]
    public async Task Upload_SheetMusicPdf_StoresPrivatelyAndReturnsSignedUrl_Async()
    {
        var result = await _sut.UploadAsync(NewRequest(), NewFile("Kinh Hoa Binh.pdf"), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ban nhac", result.Value!.Title);
        Assert.Equal("Kinh Hoa Binh.pdf", result.Value.FileName);
        Assert.Equal("signed:harmonia/music-materials/sheet-music/stored.pdf", result.Value.FileUrl);
        await _storage.Received(1).UploadAsync(
            Arg.Any<Stream>(), "Kinh Hoa Binh.pdf", "music-materials/sheet-music", true, _ct);
        await _materials.Received(1).AddAsync(
            Arg.Is<MusicMaterial>(m => m.SongId == _song.Id && m.FileSizeBytes == 1024
                && m.FilePublicId == "harmonia/music-materials/sheet-music/stored.pdf"), _ct);
        await _materials.Received(1).SaveChangesAsync(_ct);
    }

    [Theory]
    [InlineData(MaterialType.SampleAudio, "sample.mp3", "music-materials/sample-audio")]
    [InlineData(MaterialType.Lyrics, "loi.JPG", "music-materials/lyrics")]
    [InlineData(MaterialType.RehearsalMaterial, "bass.wav", "music-materials/rehearsal-material")]
    [InlineData(MaterialType.RehearsalMaterial, "tap.pdf", "music-materials/rehearsal-material")]
    public async Task Upload_AllowedExtension_GoesToFolderOfItsType_Async(MaterialType type, string fileName, string folder)
    {
        var result = await _sut.UploadAsync(NewRequest(type), NewFile(fileName), _ct);

        Assert.True(result.IsSuccess);
        await _storage.Received(1).UploadAsync(Arg.Any<Stream>(), fileName, folder, true, _ct);
    }

    [Theory]
    [InlineData(MaterialType.SheetMusic, "ban-nhac.mp3")]
    [InlineData(MaterialType.SampleAudio, "sample.pdf")]
    [InlineData(MaterialType.RehearsalMaterial, "virus.exe")]
    [InlineData(MaterialType.SheetMusic, "no-extension")]
    public async Task Upload_ExtensionNotAllowedForType_ReturnsFileTypeNotAllowed_Async(MaterialType type, string fileName)
    {
        var result = await _sut.UploadAsync(NewRequest(type), NewFile(fileName), _ct);

        Assert.Equal(ErrorCodes.MaterialFileTypeNotAllowed, result.Code);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Upload_FileOverLimit_ReturnsFileTooLarge_Async()
    {
        var result = await _sut.UploadAsync(
            NewRequest(), NewFile("big.pdf", MusicMaterialService.MaxFileSizeBytes + 1), _ct);

        Assert.Equal(ErrorCodes.MaterialFileTooLarge, result.Code);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Upload_NoFile_ReturnsFileRequired_Async()
    {
        var result = await _sut.UploadAsync(NewRequest(), null, _ct);

        Assert.Equal(ErrorCodes.MaterialFileRequired, result.Code);
    }

    [Fact]
    public async Task Upload_UnknownSong_ReturnsSongNotFound_Async()
    {
        var request = NewRequest();
        request.SongId = Guid.NewGuid();

        var result = await _sut.UploadAsync(request, NewFile("a.pdf"), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Upload_InactiveTargetSkill_ReturnsSkillInactive_Async()
    {
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Bass", IsActive = false };
        _skills.GetByIdAsync(skill.Id, _ct).Returns(skill);
        var request = NewRequest();
        request.TargetSkillId = skill.Id;

        var result = await _sut.UploadAsync(request, NewFile("a.pdf"), _ct);

        Assert.Equal(ErrorCodes.SkillInactive, result.Code);
    }

    [Fact]
    public async Task Upload_SaveFails_DeletesUploadedFileAndRethrows_Async()
    {
        _materials.SaveChangesAsync(_ct).ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UploadAsync(NewRequest(), NewFile("a.pdf"), _ct));

        await _storage.Received(1).DeleteAsync(
            "harmonia/music-materials/sheet-music/stored.pdf", true, CancellationToken.None);
    }

    [Fact]
    public async Task Upload_StorageFails_ReturnsExternalStorageFailedAndSavesNothing_Async()
    {
        _storage.UploadAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Result<FileUploadResponse>.Failure(ErrorCodes.ExternalStorageFailed));

        var result = await _sut.UploadAsync(NewRequest(), NewFile("a.pdf"), _ct);

        Assert.Equal(ErrorCodes.ExternalStorageFailed, result.Code);
        await _materials.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _materials.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Upload_SaveAndCleanupBothFail_RethrowsTheSaveError_Async()
    {
        var saveError = new InvalidOperationException("db down");
        _materials.SaveChangesAsync(_ct).ThrowsAsync(saveError);
        _storage.DeleteAsync(default!, default, default).ReturnsForAnyArgs(Result.Failure(ErrorCodes.ExternalStorageFailed));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UploadAsync(NewRequest(), NewFile("a.pdf"), _ct));

        Assert.Same(saveError, thrown);
    }

    [Fact]
    public async Task Update_Active_ChangesTitleAndSkillButKeepsFileAndType_Async()
    {
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Alto", IsActive = true };
        _skills.GetByIdAsync(skill.Id, _ct).Returns(skill);
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), SongId = _song.Id, Title = "Old", MaterialType = MaterialType.SheetMusic,
            FilePublicId = "harmonia/x.pdf", FileName = "x.pdf", IsActive = true,
        };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);

        var result = await _sut.UpdateAsync(
            material.Id, new UpdateMusicMaterialRequest { Title = " New ", TargetSkillId = skill.Id }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", result.Value!.Title);
        Assert.Equal("Alto", result.Value.TargetSkillName);
        Assert.Equal(material.Id, result.Value.Id);
        Assert.Equal("harmonia/x.pdf", material.FilePublicId);
        Assert.Equal(MaterialType.SheetMusic, material.MaterialType);
        await _materials.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_ClearsTargetSkill_WhenNull_Async()
    {
        var material = new MusicMaterial { Id = Guid.NewGuid(), Title = "T", TargetSkillId = Guid.NewGuid(), IsActive = true };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);

        var result = await _sut.UpdateAsync(material.Id, new UpdateMusicMaterialRequest { Title = "T" }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(material.TargetSkillId);
    }

    [Fact]
    public async Task Update_Deleted_ReturnsMaterialNotFound_Async()
    {
        var material = new MusicMaterial { Id = Guid.NewGuid(), IsActive = false };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);

        var result = await _sut.UpdateAsync(material.Id, new UpdateMusicMaterialRequest { Title = "T" }, _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
        await _materials.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Update_UnknownSkill_ReturnsSkillNotFound_Async()
    {
        var material = new MusicMaterial { Id = Guid.NewGuid(), Title = "Old", IsActive = true };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);

        var result = await _sut.UpdateAsync(
            material.Id, new UpdateMusicMaterialRequest { Title = "New", TargetSkillId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.SkillNotFound, result.Code);
        Assert.Equal("Old", material.Title);
    }

    [Fact]
    public async Task GetBySong_DeletedSong_ReturnsSongNotFound_Async()
    {
        _song.IsActive = false;

        var result = await _sut.GetBySongAsync(_song.Id, new PagingRequest(), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task GetBySong_MapsPageWithSignedUrls_Async()
    {
        var paging = new PagingRequest();
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), SongId = _song.Id, Title = "Audio", FileName = "a.mp3",
            FilePublicId = "harmonia/x.mp3", TargetSkill = new Skill { Name = "Tenor" },
        };
        _materials.GetActiveBySongAsync(_song.Id, paging, _ct)
            .Returns(new PagedList<MusicMaterial>([material], 1, 20, 1));

        var result = await _sut.GetBySongAsync(_song.Id, paging, _ct);

        var dto = Assert.Single(result.Value!.Items);
        Assert.Equal("signed:harmonia/x.mp3", dto.FileUrl);
        Assert.Equal("Tenor", dto.TargetSkillName);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task GetMine_Member_ReturnsMaterialsOfTheirProfileWithSignedUrls_Async()
    {
        var userId = Guid.NewGuid();
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = userId };
        _members.GetByUserIdAsync(userId, _ct).Returns(member);
        var request = new SearchMusicMaterialsRequest { SongId = _song.Id };
        var material = new MusicMaterial { Id = Guid.NewGuid(), SongId = _song.Id, Title = "Bass", FilePublicId = "harmonia/b.mp3" };
        _materials.GetActiveForMemberAsync(member.Id, null, request, _ct)
            .Returns(new PagedList<MusicMaterial>([material], 1, 20, 1));

        var result = await _sut.GetMineAsync(userId, request, _ct);

        var dto = Assert.Single(result.Value!.Items);
        Assert.Equal("signed:harmonia/b.mp3", dto.FileUrl);
        Assert.Equal(LearningStatus.NotStarted, dto.LearningStatus);
        Assert.Null(dto.LearningUpdatedAt);
    }

    [Fact]
    public async Task GetMine_MarkedMaterial_CarriesTheMembersLearningStatus_Async()
    {
        var (userId, member) = GivenMember();
        var updatedAt = DateTime.UtcNow.AddHours(-2);
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), SongId = _song.Id, Title = "Alto", FilePublicId = "harmonia/a.mp3",
            LearningProgresses =
            [
                new MaterialLearningProgress { MemberId = member.Id, Status = LearningStatus.NeedsPractice, UpdatedAt = updatedAt },
            ],
        };
        _materials.GetActiveForMemberAsync(default, default, default!, default)
            .ReturnsForAnyArgs(new PagedList<MusicMaterial>([material], 1, 20, 1));

        var result = await _sut.GetMineAsync(userId, new SearchMusicMaterialsRequest(), _ct);

        var dto = Assert.Single(result.Value!.Items);
        Assert.Equal(LearningStatus.NeedsPractice, dto.LearningStatus);
        Assert.Equal(updatedAt, dto.LearningUpdatedAt);
        Assert.Equal("signed:harmonia/a.mp3", dto.FileUrl);
    }

    [Fact]
    public async Task GetLearningProgress_ActiveMaterial_MapsMembersWithNotStartedForUnmarked_Async()
    {
        var skillId = Guid.NewGuid();
        var material = new MusicMaterial { Id = Guid.NewGuid(), TargetSkillId = skillId, IsActive = true };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);
        var request = new SearchMaterialLearningProgressRequest();
        var updatedAt = DateTime.UtcNow;
        var marked = new MemberProfile
        {
            Id = Guid.NewGuid(), FullName = "An",
            LearningProgresses = [new MaterialLearningProgress { Status = LearningStatus.Learned, UpdatedAt = updatedAt }],
        };
        var unmarked = new MemberProfile { Id = Guid.NewGuid(), FullName = "Binh" };
        _members.GetLearnersOfMaterialAsync(material.Id, skillId, request, _ct)
            .Returns(new PagedList<MemberProfile>([marked, unmarked], 1, 20, 2));

        var result = await _sut.GetLearningProgressAsync(material.Id, request, _ct);

        Assert.Equal(2, result.Value!.TotalCount);
        var rows = result.Value.Items;
        Assert.Equal((marked.Id, "An", LearningStatus.Learned, (DateTime?)updatedAt),
            (rows[0].MemberId, rows[0].FullName, rows[0].Status, rows[0].UpdatedAt));
        Assert.Equal((unmarked.Id, LearningStatus.NotStarted, (DateTime?)null),
            (rows[1].MemberId, rows[1].Status, rows[1].UpdatedAt));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetLearningProgress_MissingOrDeletedMaterial_ReturnsMaterialNotFound_Async(bool exists)
    {
        var materialId = Guid.NewGuid();
        if (exists) _materials.GetByIdAsync(materialId, _ct).Returns(new MusicMaterial { Id = materialId, IsActive = false });

        var result = await _sut.GetLearningProgressAsync(materialId, new SearchMaterialLearningProgressRequest(), _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
        await _members.DidNotReceiveWithAnyArgs().GetLearnersOfMaterialAsync(default, default, default!, _ct);
    }

    [Theory]
    [InlineData("  Hoa Binh ", "Hoa Binh")]
    [InlineData("   ", null)]
    public async Task GetMine_Keyword_IsTrimmedAndBlankBecomesNull_Async(string keyword, string? expected)
    {
        var userId = Guid.NewGuid();
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = userId };
        _members.GetByUserIdAsync(userId, _ct).Returns(member);
        var request = new SearchMusicMaterialsRequest { Keyword = keyword };
        _materials.GetActiveForMemberAsync(default, default, default!, default)
            .ReturnsForAnyArgs(new PagedList<MusicMaterial>([], 1, 20, 0));

        var result = await _sut.GetMineAsync(userId, request, _ct);

        Assert.True(result.IsSuccess);
        await _materials.Received(1).GetActiveForMemberAsync(member.Id, expected, request, _ct);
    }

    [Fact]
    public async Task UpdateLearningProgress_VisibleMaterial_UpsertsAndReturnsProgress_Async()
    {
        var (userId, member) = GivenMember();
        var materialId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;
        _materials.IsVisibleToMemberAsync(materialId, member.Id, _ct).Returns(true);
        _progresses.UpsertAsync(member.Id, materialId, LearningStatus.Learned, _ct).Returns(new MaterialLearningProgress
        {
            Id = Guid.NewGuid(), MemberId = member.Id, MaterialId = materialId,
            Status = LearningStatus.Learned, UpdatedAt = updatedAt,
        });

        var result = await _sut.UpdateLearningProgressAsync(
            userId, materialId, new UpdateMaterialLearningProgressRequest { Status = LearningStatus.Learned }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(materialId, result.Value!.MaterialId);
        Assert.Equal(LearningStatus.Learned, result.Value.Status);
        Assert.Equal(updatedAt, result.Value.UpdatedAt);
    }

    [Fact]
    public async Task UpdateLearningProgress_MaterialNotVisibleToMember_ReturnsMaterialNotFound_Async()
    {
        // Covers another skill's material, a deleted material and an unknown id alike.
        var (userId, _) = GivenMember();

        var result = await _sut.UpdateLearningProgressAsync(
            userId, Guid.NewGuid(), new UpdateMaterialLearningProgressRequest { Status = LearningStatus.Learned }, _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
        await _progresses.DidNotReceiveWithAnyArgs().UpsertAsync(default, default, default, _ct);
    }

    [Fact]
    public async Task UpdateLearningProgress_NoProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.UpdateLearningProgressAsync(
            Guid.NewGuid(), Guid.NewGuid(), new UpdateMaterialLearningProgressRequest { Status = LearningStatus.Learned }, _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _progresses.DidNotReceiveWithAnyArgs().UpsertAsync(default, default, default, _ct);
    }

    private (Guid UserId, MemberProfile Member) GivenMember()
    {
        var userId = Guid.NewGuid();
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = userId };
        _members.GetByUserIdAsync(userId, _ct).Returns(member);
        return (userId, member);
    }

    [Fact]
    public async Task GetMine_NoProfile_ReturnsMemberNotFound_Async()
    {
        var result = await _sut.GetMineAsync(Guid.NewGuid(), new SearchMusicMaterialsRequest(), _ct);

        Assert.Equal(ErrorCodes.MemberNotFound, result.Code);
        await _materials.DidNotReceiveWithAnyArgs().GetActiveForMemberAsync(default, default, default!, default);
    }

    [Fact]
    public async Task GetMine_UnknownSong_ReturnsSongNotFound_Async()
    {
        var userId = Guid.NewGuid();
        _members.GetByUserIdAsync(userId, _ct).Returns(new MemberProfile { Id = Guid.NewGuid(), UserId = userId });

        var result = await _sut.GetMineAsync(userId, new SearchMusicMaterialsRequest { SongId = Guid.NewGuid() }, _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task Delete_Active_SetsInactiveAndKeepsFile_Async()
    {
        var material = new MusicMaterial { Id = Guid.NewGuid(), IsActive = true };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);

        var result = await _sut.DeleteAsync(material.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.False(material.IsActive);
        await _storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default, default);
        await _materials.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsMaterialNotFound_Async()
    {
        var result = await _sut.DeleteAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
    }
}

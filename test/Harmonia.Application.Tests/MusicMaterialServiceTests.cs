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
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly MusicMaterialService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Song _song = new() { Id = Guid.NewGuid(), Title = "Kinh Hoa Binh", IsActive = true };

    public MusicMaterialServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MusicMaterialProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new MusicMaterialService(_materials, _songs, _skills, _storage, mapper);

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

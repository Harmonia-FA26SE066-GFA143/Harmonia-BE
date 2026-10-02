using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
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
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<SongProfile>(), NullLoggerFactory.Instance)
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
}

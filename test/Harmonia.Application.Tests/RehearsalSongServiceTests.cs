using System.Linq.Expressions;
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

public class RehearsalSongServiceTests
{
    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly IGenericRepository<Song> _songs = Substitute.For<IGenericRepository<Song>>();
    private readonly RehearsalSongService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Rehearsal _rehearsal = new() { Id = Guid.NewGuid() };
    private readonly Song _kyrie = new() { Id = Guid.NewGuid(), Title = "Kyrie" };
    private readonly Song _gloria = new() { Id = Guid.NewGuid(), Title = "Gloria" };
    private readonly Song _retired = new() { Id = Guid.NewGuid(), Title = "Retired", IsActive = false };

    public RehearsalSongServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RehearsalSongProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new RehearsalSongService(_rehearsals, _songs, mapper);

        _rehearsals.GetByIdAsync(_rehearsal.Id, _ct).Returns(_rehearsal);
        _rehearsals.GetWithSongsForUpdateAsync(_rehearsal.Id, _ct).Returns(_rehearsal);
        // Stands in for the read-back after saving: whatever the service left on the tracked rehearsal.
        _rehearsals.GetSongsAsync(_rehearsal.Id, _ct).Returns(_ => _rehearsal.Songs
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new RehearsalSong
            {
                SongId = s.SongId, DisplayOrder = s.DisplayOrder, Note = s.Note,
                Song = new[] { _kyrie, _gloria, _retired }.Single(x => x.Id == s.SongId),
            })
            .ToList());
        _songs.ListAsync(Arg.Any<Expression<Func<Song, bool>>>(), _ct)
            .Returns(call => new[] { _kyrie, _gloria, _retired }.AsQueryable()
                .Where(call.Arg<Expression<Func<Song, bool>>>()).ToList());
    }

    private static UpdateRehearsalSongsRequest Request(params (Guid SongId, string? Note)[] items) =>
        new() { Items = items.Select(i => new UpdateRehearsalSongRequest { SongId = i.SongId, Note = i.Note }).ToList() };

    private RehearsalSong Existing(Song song, int order, string? note = null)
    {
        var row = new RehearsalSong
        {
            Id = Guid.NewGuid(), RehearsalId = _rehearsal.Id, SongId = song.Id, DisplayOrder = order, Note = note,
        };
        _rehearsal.Songs.Add(row);
        return row;
    }

    [Fact]
    public async Task Get_ReturnsProgrammeInOrder_Async()
    {
        Existing(_gloria, 2);
        Existing(_kyrie, 1, "slowly");

        var result = await _sut.GetAsync(_rehearsal.Id, _ct);

        Assert.Equal(["Kyrie", "Gloria"], result.Value!.Select(s => s.SongTitle));
        Assert.Equal("slowly", result.Value![0].Note);
    }

    [Fact]
    public async Task Get_RehearsalMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.GetAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }

    [Fact]
    public async Task Update_EmptyProgramme_AddsSongsInRequestOrder_Async()
    {
        var result = await _sut.UpdateAsync(_rehearsal.Id, Request((_gloria.Id, "  verse 2  "), (_kyrie.Id, " ")), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([_gloria.Id, _kyrie.Id], result.Value!.Select(s => s.SongId));
        Assert.Equal([1, 2], result.Value!.Select(s => s.DisplayOrder));
        Assert.Equal("verse 2", result.Value![0].Note);
        Assert.Null(result.Value![1].Note);
        // New rows carry no preset key, so EF inserts them instead of treating them as updates.
        Assert.All(_rehearsal.Songs, s => Assert.Equal(Guid.Empty, s.Id));
        await _rehearsals.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_KeepsMatchingRows_RemovesTheRest_AndReorders_Async()
    {
        var kyrieRow = Existing(_kyrie, 1, "old note");
        Existing(_retired, 2);

        var result = await _sut.UpdateAsync(_rehearsal.Id, Request((_gloria.Id, null), (_kyrie.Id, "new note")), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _rehearsal.Songs.Count);
        Assert.DoesNotContain(_rehearsal.Songs, s => s.SongId == _retired.Id);
        // The kept song is the same row, updated in place.
        Assert.Same(kyrieRow, _rehearsal.Songs.Single(s => s.SongId == _kyrie.Id));
        Assert.Equal(2, kyrieRow.DisplayOrder);
        Assert.Equal("new note", kyrieRow.Note);
    }

    [Fact]
    public async Task Update_EmptyList_ClearsProgramme_Async()
    {
        Existing(_kyrie, 1);

        var result = await _sut.UpdateAsync(_rehearsal.Id, Request(), _ct);

        Assert.Empty(result.Value!);
        Assert.Empty(_rehearsal.Songs);
    }

    [Fact]
    public async Task Update_RehearsalMissing_ReturnsNotFound_Async()
    {
        var result = await _sut.UpdateAsync(Guid.NewGuid(), Request((_kyrie.Id, null)), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }

    [Fact]
    public async Task Update_SongMissing_ReturnsSongNotFound_AndSavesNothing_Async()
    {
        var result = await _sut.UpdateAsync(_rehearsal.Id, Request((_kyrie.Id, null), (Guid.NewGuid(), null)), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
        Assert.Empty(_rehearsal.Songs);
        await _rehearsals.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_AddingInactiveSong_ReturnsSongInactive_Async()
    {
        var result = await _sut.UpdateAsync(_rehearsal.Id, Request((_retired.Id, null)), _ct);

        Assert.Equal(ErrorCodes.SongInactive, result.Code);
        await _rehearsals.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_InactiveSongAlreadyOnProgramme_MayStay_Async()
    {
        Existing(_retired, 1);

        var result = await _sut.UpdateAsync(_rehearsal.Id, Request((_kyrie.Id, null), (_retired.Id, null)), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal([_kyrie.Id, _retired.Id], result.Value!.Select(s => s.SongId));
    }
}

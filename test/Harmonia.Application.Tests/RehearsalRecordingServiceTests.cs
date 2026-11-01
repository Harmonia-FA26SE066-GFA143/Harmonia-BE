using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class RehearsalRecordingServiceTests
{
    private readonly IRehearsalRecordingRepository _recordings = Substitute.For<IRehearsalRecordingRepository>();
    private readonly IRehearsalRepository _rehearsals = Substitute.For<IRehearsalRepository>();
    private readonly IMemberProfileRepository _members = Substitute.For<IMemberProfileRepository>();
    private readonly IGenericRepository<Song> _songs = Substitute.For<IGenericRepository<Song>>();
    private readonly IFileStorageService _files = Substitute.For<IFileStorageService>();
    private readonly IChoirScopeService _scope = ChoirScopeFakes.Unrestricted();
    private readonly RehearsalRecordingService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Rehearsal _rehearsal = new() { Id = Guid.NewGuid(), ChoirId = Guid.NewGuid() };
    private readonly Song _song = new() { Id = Guid.NewGuid(), Title = "Kyrie" };

    public RehearsalRecordingServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<RehearsalRecordingProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new RehearsalRecordingService(_recordings, _rehearsals, _members, _songs, _files, _scope, mapper);

        _rehearsals.GetByIdAsync(_rehearsal.Id, _ct).Returns(_rehearsal);
        _songs.GetByIdAsync(_song.Id, _ct).Returns(_song);
        _files.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), "rehearsal-recordings", true, _ct)
            .Returns(ci => Result<FileUploadResponse>.Success(
                new FileUploadResponse($"harmonia/rehearsal-recordings/{Guid.NewGuid()}{Path.GetExtension(ci.ArgAt<string>(1))}", "url")));
        _files.GetSignedUrl(Arg.Any<string>()).Returns(ci => $"signed:{ci.Arg<string>()}");
    }

    private static FileContent Audio(string name = "take.webm", long length = 1024) =>
        new(new MemoryStream([1, 2, 3]), name, length);

    // ---- Create ----

    [Fact]
    public async Task Create_ValidWebm_StoresPrivateFileAndReturnsSignedUrl_Async()
    {
        var request = new CreateRehearsalRecordingRequest { SongId = _song.Id, Title = "  Lần 1  ", DurationSeconds = 95 };

        var result = await _sut.CreateAsync(_rehearsal.Id, request, Audio(), _ct);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal((_rehearsal.Id, _song.Id, "Kyrie", "Lần 1", "take.webm", 1024L, 95),
            (dto.RehearsalId, dto.SongId, dto.SongTitle, dto.Title, dto.FileName, dto.FileSizeBytes, dto.DurationSeconds));
        Assert.StartsWith("signed:harmonia/rehearsal-recordings/", dto.AudioUrl);
        await _recordings.Received(1).AddAsync(Arg.Is<RehearsalRecording>(r => r.RehearsalId == _rehearsal.Id), _ct);
        await _recordings.Received(1).SaveChangesAsync(_ct);
    }

    [Theory]
    [InlineData("take.exe", 1024L, ErrorCodes.RehearsalRecordingTypeNotAllowed)]
    [InlineData("take.webm", 0L, ErrorCodes.RehearsalRecordingRequired)]
    [InlineData("take.m4a", RehearsalRecordingService.MaxAudioSizeBytes + 1, ErrorCodes.RehearsalRecordingTooLarge)]
    public async Task Create_InvalidFile_ReturnsCodeWithoutUpload_Async(string name, long length, string code)
    {
        var result = await _sut.CreateAsync(_rehearsal.Id, new CreateRehearsalRecordingRequest(), Audio(name, length), _ct);

        Assert.Equal(code, result.Code);
        await _files.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, _ct);
    }

    [Fact]
    public async Task Create_NoFile_ReturnsRequired_Async()
    {
        var result = await _sut.CreateAsync(_rehearsal.Id, new CreateRehearsalRecordingRequest(), null, _ct);

        Assert.Equal(ErrorCodes.RehearsalRecordingRequired, result.Code);
    }

    [Fact]
    public async Task Create_RehearsalOfAnotherDirector_ReturnsRehearsalNotFound_Async()
    {
        _scope.CanManageChoirAsync(_rehearsal.ChoirId, _ct).Returns(false);

        var result = await _sut.CreateAsync(_rehearsal.Id, new CreateRehearsalRecordingRequest(), Audio(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
        await _files.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, _ct);
    }

    [Fact]
    public async Task Create_UnknownSong_ReturnsSongNotFound_Async()
    {
        var request = new CreateRehearsalRecordingRequest { SongId = Guid.NewGuid() };

        var result = await _sut.CreateAsync(_rehearsal.Id, request, Audio(), _ct);

        Assert.Equal(ErrorCodes.SongNotFound, result.Code);
    }

    [Fact]
    public async Task Create_SaveFails_DeletesUploadedFile_Async()
    {
        _recordings.SaveChangesAsync(_ct).Returns(Task.FromException(new InvalidOperationException("db down")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(_rehearsal.Id, new CreateRehearsalRecordingRequest(), Audio(), _ct));

        await _files.Received(1).DeleteAsync(Arg.Is<string>(id => id.EndsWith(".webm")), true, CancellationToken.None);
    }

    // ---- Get ----

    [Fact]
    public async Task Get_Director_ReturnsRecordingsWithSignedUrls_Async()
    {
        var recording = new RehearsalRecording
        {
            Id = Guid.NewGuid(), RehearsalId = _rehearsal.Id, AudioPublicId = "harmonia/rehearsal-recordings/a.webm", FileName = "a.webm",
        };
        _recordings.GetByRehearsalAsync(_rehearsal.Id, _ct).Returns([recording]);

        var result = await _sut.GetAsync(_userId, isDirector: true, _rehearsal.Id, _ct);

        Assert.Equal("signed:harmonia/rehearsal-recordings/a.webm", Assert.Single(result.Value!).AudioUrl);
    }

    [Fact]
    public async Task Get_MissingRehearsal_ReturnsRehearsalNotFound_Async()
    {
        var result = await _sut.GetAsync(_userId, isDirector: true, Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }

    [Fact]
    public async Task Get_MemberOfTheChoir_SeesRecordings_Async()
    {
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = _userId };
        IReadOnlyCollection<Guid> choirIds = [_rehearsal.ChoirId!.Value];
        _members.GetByUserIdAsync(_userId, _ct).Returns(member);
        _scope.GetMemberChoirIdsAsync(member.Id, _ct).Returns(choirIds);
        _rehearsals.GetVisibleForMemberAsync(_rehearsal.Id, member.Id, choirIds, _ct).Returns(_rehearsal);
        _recordings.GetByRehearsalAsync(_rehearsal.Id, _ct).Returns([]);

        var result = await _sut.GetAsync(_userId, isDirector: false, _rehearsal.Id, _ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Get_MemberOutsideTheChoir_ReturnsRehearsalNotFound_Async()
    {
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = _userId };
        _members.GetByUserIdAsync(_userId, _ct).Returns(member);
        _scope.GetMemberChoirIdsAsync(member.Id, _ct).Returns(Array.Empty<Guid>());

        var result = await _sut.GetAsync(_userId, isDirector: false, _rehearsal.Id, _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
        await _recordings.DidNotReceiveWithAnyArgs().GetByRehearsalAsync(default, _ct);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesRowThenFile_Async()
    {
        var recording = new RehearsalRecording { Id = Guid.NewGuid(), RehearsalId = _rehearsal.Id, AudioPublicId = "x.webm" };
        _recordings.GetByIdAsync(recording.Id, _ct).Returns(recording);

        var result = await _sut.DeleteAsync(_rehearsal.Id, recording.Id, _ct);

        Assert.True(result.IsSuccess);
        _recordings.Received(1).Remove(recording);
        await _recordings.Received(1).SaveChangesAsync(_ct);
        await _files.Received(1).DeleteAsync("x.webm", true, CancellationToken.None);
    }

    [Fact]
    public async Task Delete_RecordingOfAnotherRehearsal_ReturnsRecordingNotFound_Async()
    {
        var recording = new RehearsalRecording { Id = Guid.NewGuid(), RehearsalId = Guid.NewGuid(), AudioPublicId = "x.webm" };
        _recordings.GetByIdAsync(recording.Id, _ct).Returns(recording);

        var result = await _sut.DeleteAsync(_rehearsal.Id, recording.Id, _ct);

        Assert.Equal(ErrorCodes.RehearsalRecordingNotFound, result.Code);
        _recordings.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task Delete_RehearsalOfAnotherDirector_ReturnsRehearsalNotFound_Async()
    {
        _scope.CanManageChoirAsync(_rehearsal.ChoirId, _ct).Returns(false);

        var result = await _sut.DeleteAsync(_rehearsal.Id, Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.RehearsalNotFound, result.Code);
    }
}

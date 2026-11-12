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

public class LyricsExtractionServiceTests
{
    private readonly ILyricsExtractionJobRepository _jobs = Substitute.For<ILyricsExtractionJobRepository>();
    private readonly IMusicMaterialRepository _materials = Substitute.For<IMusicMaterialRepository>();
    private readonly IMusicMaterialService _materialService = Substitute.For<IMusicMaterialService>();
    private readonly ILyricsExtractionQueue _queue = Substitute.For<ILyricsExtractionQueue>();
    private readonly LyricsExtractionService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LyricsExtractionServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LyricsExtractionJobProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LyricsExtractionService(_jobs, _materials, _materialService, _queue, mapper);
    }

    private MusicMaterial StubMaterial(MaterialType type = MaterialType.SheetMusic)
    {
        var material = new MusicMaterial
        {
            Id = Guid.NewGuid(), Title = "Ban nhac", MaterialType = type, FilePublicId = "harmonia/sheet.pdf", IsActive = true,
        };
        _materials.GetByIdAsync(material.Id, _ct).Returns(material);
        return material;
    }

    private LyricsExtractionJob StubJob(Guid materialId, LyricsExtractionStatus status, DateTime? expiresAt = null)
    {
        var job = new LyricsExtractionJob
        {
            Id = Guid.NewGuid(), MusicMaterialId = materialId, Status = status, ExpiresAt = expiresAt,
        };
        _jobs.GetByIdAsync(job.Id, _ct).Returns(job);
        return job;
    }

    [Fact]
    public async Task Start_SheetMusic_SavesQueuedJobThenWakesWorker_Async()
    {
        var material = StubMaterial();

        var result = await _sut.StartAsync(material.Id, _ct);

        Assert.Equal(LyricsExtractionStatus.Queued, result.Value!.Status);
        Received.InOrder(() =>
        {
            _jobs.AddAsync(Arg.Is<LyricsExtractionJob>(j => j.MusicMaterialId == material.Id), _ct);
            _jobs.SaveChangesAsync(_ct);
            _queue.Enqueue(result.Value.Id);
        });
    }

    [Fact]
    public async Task Start_JobAlreadyActive_ReturnsItWithoutNewJob_Async()
    {
        var material = StubMaterial();
        var running = new LyricsExtractionJob { Id = Guid.NewGuid(), MusicMaterialId = material.Id, Status = LyricsExtractionStatus.Running };
        _jobs.GetActiveByMaterialAsync(material.Id, _ct).Returns(running);

        var result = await _sut.StartAsync(material.Id, _ct);

        Assert.Equal(running.Id, result.Value!.Id);
        await _jobs.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _queue.DidNotReceiveWithAnyArgs().Enqueue(default);
    }

    [Fact]
    public async Task Start_NotSheetMusic_ReturnsNotSheetMusic_Async()
    {
        var material = StubMaterial(MaterialType.SampleAudio);

        var result = await _sut.StartAsync(material.Id, _ct);

        Assert.Equal(ErrorCodes.MaterialNotSheetMusic, result.Code);
        await _jobs.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Start_MissingMaterial_ReturnsMaterialNotFound_Async()
    {
        var result = await _sut.StartAsync(Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.MaterialNotFound, result.Code);
    }

    [Fact]
    public async Task Get_JobOfAnotherMaterial_ReturnsNotFound_Async()
    {
        var job = StubJob(Guid.NewGuid(), LyricsExtractionStatus.Queued);

        var result = await _sut.GetAsync(Guid.NewGuid(), job.Id, _ct);

        Assert.Equal(ErrorCodes.LyricsExtractionNotFound, result.Code);
    }

    [Fact]
    public async Task Get_ExpiredJob_ReturnsNotFound_Async()
    {
        var materialId = Guid.NewGuid();
        var job = StubJob(materialId, LyricsExtractionStatus.Succeeded, DateTime.UtcNow.AddMinutes(-1));

        var result = await _sut.GetAsync(materialId, job.Id, _ct);

        Assert.Equal(ErrorCodes.LyricsExtractionNotFound, result.Code);
    }

    [Fact]
    public async Task Process_AiSucceeds_StoresLyricsAndRetention_Async()
    {
        var job = StubJob(Guid.NewGuid(), LyricsExtractionStatus.Queued);
        _materialService.ExtractLyricsAsync(job.MusicMaterialId, _ct)
            .Returns(Result<ExtractedLyricsResponse>.Success(new ExtractedLyricsResponse("Xin vang loi Chua")));

        await _sut.ProcessAsync(job.Id, _ct);

        Assert.Equal(LyricsExtractionStatus.Succeeded, job.Status);
        Assert.Equal("Xin vang loi Chua", job.LyricsText);
        Assert.Null(job.ErrorCode);
        Assert.Equal(job.CompletedAt + LyricsExtractionService.Retention, job.ExpiresAt);
        await _jobs.Received(2).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Process_AiFails_StoresTheCode_Async()
    {
        var job = StubJob(Guid.NewGuid(), LyricsExtractionStatus.Queued);
        _materialService.ExtractLyricsAsync(job.MusicMaterialId, _ct)
            .Returns(Result<ExtractedLyricsResponse>.Failure(ErrorCodes.ExternalAiFailed));

        await _sut.ProcessAsync(job.Id, _ct);

        Assert.Equal(LyricsExtractionStatus.Failed, job.Status);
        Assert.Equal(ErrorCodes.ExternalAiFailed, job.ErrorCode);
        Assert.Null(job.LyricsText);
    }

    [Fact]
    public async Task Process_JobNotQueued_SkipsAi_Async()
    {
        var job = StubJob(Guid.NewGuid(), LyricsExtractionStatus.Succeeded);

        await _sut.ProcessAsync(job.Id, _ct);

        await _materialService.DidNotReceiveWithAnyArgs().ExtractLyricsAsync(default, default);
    }

    [Fact]
    public async Task Recover_FailsRunningJobs_ThenReturnsQueued_Async()
    {
        List<Guid> queued = [Guid.NewGuid()];
        _jobs.ListQueuedIdsAsync(_ct).Returns(queued);

        var result = await _sut.RecoverAsync(_ct);

        Assert.Equal(queued, result);
        await _jobs.Received(1).FailRunningAsync(ErrorCodes.LyricsExtractionInterrupted, Arg.Any<DateTime>(), Arg.Any<DateTime>(), _ct);
    }
}

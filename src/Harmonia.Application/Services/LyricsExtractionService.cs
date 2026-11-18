using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class LyricsExtractionService(
    ILyricsExtractionJobRepository jobRepository,
    IMusicMaterialRepository musicMaterialRepository,
    IMusicMaterialService musicMaterialService,
    ILyricsExtractionQueue queue,
    IMapper mapper) : ILyricsExtractionService
{
    /// <summary>How long a finished job's result can be read again.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<Result<LyricsExtractionJobDto>> StartAsync(Guid musicMaterialId, CancellationToken cancellationToken)
    {
        // Checked here as well as in the worker, so a wrong material fails the request instead of a job.
        var material = await musicMaterialRepository.GetByIdAsync(musicMaterialId, cancellationToken);
        if (material is not { IsActive: true }) return Result<LyricsExtractionJobDto>.Failure(ErrorCodes.MaterialNotFound);
        if (material.MaterialType != MaterialType.SheetMusic || material.FilePublicId is null)
            return Result<LyricsExtractionJobDto>.Failure(ErrorCodes.MaterialNotSheetMusic);

        // shortcut: two requests at the same instant can both start a job; add a filtered unique index if that shows up.
        var active = await jobRepository.GetActiveByMaterialAsync(musicMaterialId, cancellationToken);
        if (active is not null) return Result<LyricsExtractionJobDto>.Success(mapper.Map<LyricsExtractionJobDto>(active));

        // Cleanup rides on new work, so an idle app never wakes the database just to delete rows.
        await jobRepository.DeleteExpiredAsync(DateTime.UtcNow, cancellationToken);

        var job = new LyricsExtractionJob
        {
            Id = Guid.NewGuid(), MusicMaterialId = musicMaterialId, Status = LyricsExtractionStatus.Queued
        };
        await jobRepository.AddAsync(job, cancellationToken);
        await jobRepository.SaveChangesAsync(cancellationToken);
        queue.Enqueue(job.Id);

        return Result<LyricsExtractionJobDto>.Success(mapper.Map<LyricsExtractionJobDto>(job));
    }

    public async Task<Result<LyricsExtractionJobDto>> GetAsync(
        Guid musicMaterialId, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken);
        return job is null || job.MusicMaterialId != musicMaterialId || job.ExpiresAt <= DateTime.UtcNow
            ? Result<LyricsExtractionJobDto>.Failure(ErrorCodes.LyricsExtractionNotFound)
            : Result<LyricsExtractionJobDto>.Success(mapper.Map<LyricsExtractionJobDto>(job));
    }

    public async Task<Result<LyricsExtractionJobDto>> GetLatestAsync(Guid musicMaterialId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetLatestByMaterialAsync(musicMaterialId, DateTime.UtcNow, cancellationToken);
        return job is null
            ? Result<LyricsExtractionJobDto>.Failure(ErrorCodes.LyricsExtractionNotFound)
            : Result<LyricsExtractionJobDto>.Success(mapper.Map<LyricsExtractionJobDto>(job));
    }

    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is not { Status: LyricsExtractionStatus.Queued }) return;

        job.Status = LyricsExtractionStatus.Running;
        await jobRepository.SaveChangesAsync(cancellationToken);

        // Re-checks the material: it may have been deleted or replaced while the job waited.
        var lyrics = await musicMaterialService.ExtractLyricsAsync(job.MusicMaterialId, cancellationToken);
        Complete(job, lyrics.IsSuccess ? lyrics.Value!.LyricsText : null, lyrics.Code);
        await jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid jobId, string errorCode, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null || job.Status is LyricsExtractionStatus.Succeeded or LyricsExtractionStatus.Failed) return;

        Complete(job, null, errorCode);
        await jobRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Guid>> RecoverAsync(CancellationToken cancellationToken)
    {
        // shortcut: assumes one app instance (Azure F1); with several, one instance's restart would fail the others' jobs.
        var now = DateTime.UtcNow;
        await jobRepository.FailRunningAsync(ErrorCodes.LyricsExtractionInterrupted, now, now + Retention, cancellationToken);
        return await jobRepository.ListQueuedIdsAsync(cancellationToken);
    }

    private static void Complete(LyricsExtractionJob job, string? lyricsText, string? errorCode)
    {
        job.Status = errorCode is null ? LyricsExtractionStatus.Succeeded : LyricsExtractionStatus.Failed;
        job.LyricsText = lyricsText;
        job.ErrorCode = errorCode;
        job.CompletedAt = DateTime.UtcNow;
        job.ExpiresAt = job.CompletedAt + Retention;
    }
}

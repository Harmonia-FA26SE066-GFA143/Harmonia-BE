using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>AI lyrics extraction from sheet music, run as a background job that outlives the request.</summary>
public interface ILyricsExtractionService
{
    /// <summary>
    /// Queues an extraction of the sheet music material. When the material already has a Queued or Running job,
    /// returns that job instead of starting another.
    /// </summary>
    Task<Result<LyricsExtractionJobDto>> StartAsync(Guid musicMaterialId, CancellationToken cancellationToken);

    Task<Result<LyricsExtractionJobDto>> GetAsync(Guid musicMaterialId, Guid jobId, CancellationToken cancellationToken);

    /// <summary>Newest unexpired job of the material, so a reloaded page finds it without keeping the job id.</summary>
    Task<Result<LyricsExtractionJobDto>> GetLatestAsync(Guid musicMaterialId, CancellationToken cancellationToken);

    /// <summary>Worker only: runs a Queued job to completion. Any other status is skipped.</summary>
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Worker only: marks a job Failed after an unexpected error, so it does not stay Running.</summary>
    Task FailAsync(Guid jobId, string errorCode, CancellationToken cancellationToken);

    /// <summary>
    /// Worker only, at startup: fails the jobs a restart cut off and returns the Queued job ids to run.
    /// </summary>
    Task<List<Guid>> RecoverAsync(CancellationToken cancellationToken);
}

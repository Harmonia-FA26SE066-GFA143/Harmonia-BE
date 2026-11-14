using System.Threading.Channels;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harmonia.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs lyrics extraction jobs one at a time. Woken by <see cref="Enqueue"/> instead of polling, so an idle app
/// never touches the database: the free serverless Azure SQL can pause and its monthly allowance is not spent.
/// Queued jobs left by a restart are picked up once at startup.
/// </summary>
public class LyricsExtractionWorker(IServiceScopeFactory scopeFactory, ILogger<LyricsExtractionWorker> logger)
    : BackgroundService, ILyricsExtractionQueue
{
    private readonly Channel<Guid> _jobIds = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid jobId) => _jobIds.Writer.TryWrite(jobId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var pending = await scope.ServiceProvider.GetRequiredService<ILyricsExtractionService>().RecoverAsync(stoppingToken);
            pending.ForEach(Enqueue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort, like DataSeeder: a database still resuming must not stop new jobs from running.
            logger.LogError(ex, "Recovering lyrics extraction jobs failed.");
        }

        // A job id read twice (startup plus a request) is harmless: ProcessAsync skips anything no longer Queued.
        await foreach (var jobId in _jobIds.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(jobId, stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid jobId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ILyricsExtractionService>().ProcessAsync(jobId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: the job stays Running and RecoverAsync fails it on the next start.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lyrics extraction job {JobId} failed unexpectedly.", jobId);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ILyricsExtractionService>()
                    .FailAsync(jobId, ErrorCodes.InternalError, stoppingToken);
            }
            catch (Exception failEx) when (failEx is not OperationCanceledException)
            {
                logger.LogError(failEx, "Could not mark lyrics extraction job {JobId} failed.", jobId);
            }
        }
    }
}

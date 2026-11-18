using System.Threading.Channels;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Harmonia.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends the day's participation reminders when someone first uses the app from 07:00 parish time. The free App
/// Service plan unloads an idle app, so a timer cannot be relied on; a request means the app and the serverless
/// database are awake anyway. The work itself is in <see cref="IEventParticipationService.SendDueRemindersAsync"/>.
/// </summary>
public class ParticipationReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ParticipationReminderWorker> logger)
    : BackgroundService, IParticipationReminderTrigger
{
    private static readonly TimeOnly EarliestRun = new(7, 0);

    private readonly Channel<bool> _runs = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly object _gate = new();
    private DateOnly? _lastRunDate;

    public void RequestDailyRun()
    {
        if (TryClaimRun(VietnamTime.Now))
        {
            _runs.Writer.TryWrite(true);
        }
    }

    /// <summary>
    /// True for the first call on a day at or after 07:00 (<paramref name="now"/> is parish wall-clock). The date is kept
    /// in memory only: after a restart the day runs again, and the service skips members already reminded that day.
    /// </summary>
    public bool TryClaimRun(DateTime now)
    {
        if (TimeOnly.FromDateTime(now) < EarliestRun)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now);
        lock (_gate)
        {
            if (_lastRunDate == today)
            {
                return false;
            }

            _lastRunDate = today;
            return true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _runs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reminded = await scope.ServiceProvider.GetRequiredService<IEventParticipationService>()
                    .SendDueRemindersAsync(stoppingToken);
                logger.LogInformation("Sent participation reminders to {Count} members.", reminded);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A database still resuming must not stop the app; the next day runs again.
                logger.LogError(ex, "Sending participation reminders failed.");
            }
        }
    }
}

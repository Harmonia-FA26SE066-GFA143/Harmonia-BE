using Harmonia.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Infrastructure.Tests;

public class ParticipationReminderWorkerTests
{
    private readonly ParticipationReminderWorker _worker = new(
        Substitute.For<IServiceScopeFactory>(), NullLogger<ParticipationReminderWorker>.Instance);

    [Fact]
    public void TryClaimRun_OnlyTheFirstCallFrom0700EachDayRuns()
    {
        var day = new DateTime(2026, 11, 15);

        Assert.False(_worker.TryClaimRun(day.AddHours(6).AddMinutes(59)));
        Assert.True(_worker.TryClaimRun(day.AddHours(7)));
        Assert.False(_worker.TryClaimRun(day.AddHours(15)));
        Assert.True(_worker.TryClaimRun(day.AddDays(1).AddHours(8)));
    }
}

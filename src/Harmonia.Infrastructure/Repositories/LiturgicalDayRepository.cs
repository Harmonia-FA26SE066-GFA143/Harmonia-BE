using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Infrastructure.Repositories;

public class LiturgicalDayRepository(HarmoniaDbContext dbContext)
    : GenericRepository<LiturgicalDay>(dbContext), ILiturgicalDayRepository
{
    public Task<LiturgicalDay?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        DbContext.LiturgicalDays.FirstOrDefaultAsync(x => x.Date == date, cancellationToken);

    public async Task<HashSet<DateOnly>> GetCachedDatesInYearAsync(int year, CancellationToken cancellationToken)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        var dates = await DbContext.LiturgicalDays
            .Where(x => x.Date >= start && x.Date <= end)
            .Select(x => x.Date)
            .ToListAsync(cancellationToken);

        return dates.ToHashSet();
    }

    public async Task AddRangeAsync(IEnumerable<LiturgicalDay> liturgicalDays, CancellationToken cancellationToken) =>
        await DbContext.LiturgicalDays.AddRangeAsync(liturgicalDays, cancellationToken);
}
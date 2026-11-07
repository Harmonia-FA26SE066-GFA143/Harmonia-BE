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

    public Task<List<LiturgicalDay>> GetRangeAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        DbContext.LiturgicalDays
            .AsNoTracking()
            .Where(x => x.Date >= fromDate && x.Date <= toDate)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

    public Task<List<LiturgicalDay>> GetYearForUpdateAsync(int year, CancellationToken cancellationToken)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return DbContext.LiturgicalDays
            .Where(x => x.Date >= start && x.Date <= end)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<LiturgicalDay> liturgicalDays, CancellationToken cancellationToken) =>
        await DbContext.LiturgicalDays.AddRangeAsync(liturgicalDays, cancellationToken);
}
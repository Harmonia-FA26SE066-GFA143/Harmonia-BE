using Harmonia.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILiturgicalDayRepository : IGenericRepository<LiturgicalDay>
{
    Task<LiturgicalDay?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task<HashSet<DateOnly>> GetCachedDatesInYearAsync(int year, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<LiturgicalDay> liturgicalDays, CancellationToken cancellationToken);
}
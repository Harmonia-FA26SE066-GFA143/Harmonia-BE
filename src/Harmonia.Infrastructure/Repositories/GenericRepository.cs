using System.Linq.Expressions;
using Harmonia.Application.Common.Models;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Common;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class GenericRepository<T>(HarmoniaDbContext dbContext) : IGenericRepository<T> where T : BaseEntity
{
    protected HarmoniaDbContext DbContext { get; } = dbContext;

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.Set<T>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) =>
        DbContext.Set<T>().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken) =>
        await DbContext.Set<T>().AddAsync(entity, cancellationToken);

    public virtual void Remove(T entity) => DbContext.Set<T>().Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        DbContext.SaveChangesAsync(cancellationToken);

    public Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> operation, CancellationToken cancellationToken) =>
        // A retrying execution strategy (SQL Server) refuses a user transaction unless it wraps the whole unit.
        DbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation();
                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }

                await transaction.RollbackAsync(cancellationToken);
                DbContext.ChangeTracker.Clear();
                return result;
            }
            catch
            {
                // Tracked entities still hold the rolled-back values; a retry must reload them.
                DbContext.ChangeTracker.Clear();
                throw;
            }
        });
}

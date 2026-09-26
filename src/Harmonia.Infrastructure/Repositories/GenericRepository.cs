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

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken) =>
        await DbContext.Set<T>().AddAsync(entity, cancellationToken);

    public virtual void Remove(T entity) => DbContext.Set<T>().Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        DbContext.SaveChangesAsync(cancellationToken);
}
